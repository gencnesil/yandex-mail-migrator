using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailMigration.Application;
using MailMigration.Domain;
using MimeKit;

namespace MailMigration.Infrastructure;

public sealed class MessageMigrationService(IMigrationStore store, ICredentialStorageService credentials, IPauseController pause) : IMessageMigrationService
{
    private sealed record WorkItem(FolderMapping Mapping, uint UidValidity, UniqueId Uid, MessageFlags Flags, DateTimeOffset? InternalDate, long Size, string? MessageId, string Fingerprint);

    public async Task RunAsync(MigrationJob job, MailAccount source, MailAccount target, IReadOnlyList<FolderMapping> mappings, IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
    {
        job.Status = MigrationStatus.Running;
        job.StartedAt ??= DateTimeOffset.UtcNow;
        await store.SaveJobAsync(job, cancellationToken);
        var workerCount = Math.Clamp(job.Parallelism, 1, 10);
        var channel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(Math.Max(job.BatchSize * 2, 20)) { FullMode = BoundedChannelFullMode.Wait, SingleWriter = true });
        long processed = 0, completed = 0, skipped = 0, failed = 0, bytes = 0, total = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var producer = ProduceAsync(channel.Writer, job, source, mappings, value => Interlocked.Exchange(ref total, value), cancellationToken);
            var workers = Enumerable.Range(0, workerCount).Select(_ => ConsumeAsync(channel.Reader, job, source, target, progress, stopwatch,
                () => (Interlocked.Read(ref processed), Interlocked.Read(ref total), Interlocked.Read(ref completed), Interlocked.Read(ref skipped), Interlocked.Read(ref failed), Interlocked.Read(ref bytes)),
                () => Interlocked.Increment(ref processed), () => Interlocked.Increment(ref completed), () => Interlocked.Increment(ref skipped), () => Interlocked.Increment(ref failed), n => Interlocked.Add(ref bytes, n), cancellationToken)).ToArray();
            await producer;
            await Task.WhenAll(workers);
            job.Status = MigrationStatus.Verifying;
            await store.SaveJobAsync(job, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            job.Status = MigrationStatus.Cancelled;
            job.LastError = "Kullanıcı tarafından güvenli şekilde durduruldu.";
            await store.SaveJobAsync(job, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            job.Status = MigrationStatus.Failed;
            job.LastError = ex.Message;
            await store.SaveJobAsync(job, CancellationToken.None);
            throw;
        }
    }

    private async Task ProduceAsync(ChannelWriter<WorkItem> writer, MigrationJob job, MailAccount source, IReadOnlyList<FolderMapping> mappings, Action<long> setTotal, CancellationToken cancellationToken)
    {
        Exception? error = null;
        try
        {
            using var client = await ConnectAsync(source, cancellationToken);
            long total = 0;
            foreach (var mapping in mappings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var folder = await client.GetFolderAsync(mapping.SourcePath, cancellationToken);
                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
                var uids = await folder.SearchAsync(SearchQuery.All, cancellationToken);
                total += uids.Count;
                setTotal(total);
                for (var offset = 0; offset < uids.Count; offset += Math.Clamp(job.BatchSize, 10, 1000))
                {
                    await pause.WaitIfPausedAsync(cancellationToken);
                    var page = uids.Skip(offset).Take(job.BatchSize).ToList();
                    var summaries = await folder.FetchAsync(page, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate | MessageSummaryItems.Size | MessageSummaryItems.Envelope, cancellationToken);
                    foreach (var summary in summaries)
                    {
                        var fingerprint = Fingerprint(summary.Envelope?.MessageId, summary.InternalDate, summary.Size ?? 0, summary.Envelope?.Subject, summary.Envelope?.From?.ToString());
                        await writer.WriteAsync(new(mapping, folder.UidValidity, summary.UniqueId, summary.Flags ?? MessageFlags.None, summary.InternalDate, summary.Size ?? 0, summary.Envelope?.MessageId, fingerprint), cancellationToken);
                    }
                }
                await folder.CloseAsync(false, cancellationToken);
            }
        }
        catch (Exception ex) { error = ex; throw; }
        finally { writer.TryComplete(error); }
    }

    private async Task ConsumeAsync(ChannelReader<WorkItem> reader, MigrationJob job, MailAccount source, MailAccount target, IProgress<MigrationProgress>? progress, Stopwatch stopwatch,
        Func<(long processed, long total, long completed, long skipped, long failed, long bytes)> snapshot, Action incProcessed, Action incCompleted, Action incSkipped, Action incFailed, Action<long> addBytes, CancellationToken cancellationToken)
    {
        using var sourceClient = await ConnectAsync(source, cancellationToken);
        using var targetClient = await ConnectAsync(target, cancellationToken);
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            await pause.WaitIfPausedAsync(cancellationToken);
            if (await store.IsCompletedAsync(job.Id, item.Mapping.SourcePath, item.UidValidity, item.Uid.Id, item.Fingerprint, cancellationToken)) { incSkipped(); incProcessed(); Report(item.Mapping.SourcePath); continue; }
            var record = new MigrationMessage { JobId = job.Id, SourceFolderPath = item.Mapping.SourcePath, TargetFolderPath = item.Mapping.TargetPath, SourceUid = item.Uid.Id, SourceUidValidity = item.UidValidity, MessageId = item.MessageId, Fingerprint = item.Fingerprint, Size = item.Size, MessageDate = item.InternalDate, StartedAt = DateTimeOffset.UtcNow };
            var detectedDuplicate = false;
            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    await EnsureConnectedAsync(sourceClient, source, cancellationToken);
                    await EnsureConnectedAsync(targetClient, target, cancellationToken);
                    record.AttemptCount++;
                    record.Status = MessageMigrationStatus.Downloading;
                    await store.SaveMessageAsync(record, cancellationToken);
                    var temp = Path.Combine(Path.GetTempPath(), $"mailmigration-{job.Id:N}-{item.Uid.Id}.eml");
                    try
                    {
                        var sourceFolder = await sourceClient.GetFolderAsync(item.Mapping.SourcePath, cancellationToken);
                        if (!sourceFolder.IsOpen) await sourceFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
                        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        await using (var input = await sourceFolder.GetStreamAsync(item.Uid, cancellationToken)) await input.CopyToAsync(output, 1024 * 128, cancellationToken);
                        record.Status = MessageMigrationStatus.Uploading;
                        await store.SaveMessageAsync(record, cancellationToken);
                        await using var inputFile = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        var message = await MimeMessage.LoadAsync(inputFile, cancellationToken);
                        var targetFolder = await GetOrCreateFolderAsync(targetClient, item.Mapping.TargetPath, cancellationToken);
                        if (!targetFolder.IsOpen) await targetFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
                        var existingUid = await FindExistingAsync(targetFolder, item, cancellationToken);
                        if (existingUid is not null) { record.TargetUid = existingUid.Value.Id; detectedDuplicate = true; return; }
                        var targetUid = await targetFolder.AppendAsync(message, item.Flags & SettableFlags, item.InternalDate ?? message.Date, cancellationToken, null);
                        record.TargetUid = targetUid?.Id;
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }, job.MaximumRetries, cancellationToken);
                record.Status = detectedDuplicate ? MessageMigrationStatus.Skipped : MessageMigrationStatus.Completed;
                record.CompletedAt = DateTimeOffset.UtcNow;
                record.LastError = null;
                await store.SaveMessageAsync(record, cancellationToken);
                await store.SaveCheckpointAsync(new() { JobId = job.Id, FolderPath = item.Mapping.SourcePath, UidValidity = item.UidValidity, LastCompletedUid = item.Uid.Id }, cancellationToken);
                if (detectedDuplicate) incSkipped(); else { incCompleted(); addBytes(item.Size); }
            }
            catch (OperationCanceledException) { record.Status = MessageMigrationStatus.Cancelled; await store.SaveMessageAsync(record, CancellationToken.None); throw; }
            catch (Exception ex) { record.Status = MessageMigrationStatus.Failed; record.LastError = $"{ex.GetType().Name}: {ex.Message}"; await store.SaveMessageAsync(record, CancellationToken.None); incFailed(); }
            finally { incProcessed(); Report(item.Mapping.SourcePath); }
        }

        void Report(string folder)
        {
            var s = snapshot();
            progress?.Report(new(job.Id, folder, s.processed, s.total, s.completed, s.skipped, s.failed, s.bytes, s.processed / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.1)));
        }
    }

    private async Task<ImapClient> ConnectAsync(MailAccount account, CancellationToken cancellationToken)
    {
        var client = new ImapClient { Timeout = account.Imap.TimeoutSeconds * 1000 };
        client.ServerCertificateValidationCallback = (_, _, _, errors) => errors == System.Net.Security.SslPolicyErrors.None || account.Imap.AcceptInvalidCertificate;
        await client.ConnectAsync(account.Imap.Host, account.Imap.Port, account.Imap.Security.ToSocketOptions(), cancellationToken);
        await client.AuthenticateAsync(account.Username, await credentials.GetAsync(account.CredentialKey, cancellationToken) ?? throw new InvalidOperationException("Kimlik bilgisi bulunamadı."), cancellationToken);
        return client;
    }

    private async Task EnsureConnectedAsync(ImapClient client, MailAccount account, CancellationToken cancellationToken)
    {
        if (!client.IsConnected) await client.ConnectAsync(account.Imap.Host, account.Imap.Port, account.Imap.Security.ToSocketOptions(), cancellationToken);
        if (!client.IsAuthenticated) await client.AuthenticateAsync(account.Username, await credentials.GetAsync(account.CredentialKey, cancellationToken) ?? throw new InvalidOperationException("Kimlik bilgisi bulunamadı."), cancellationToken);
    }

    private static async Task<UniqueId?> FindExistingAsync(IMailFolder folder, WorkItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.MessageId)) return null;
        var candidates = await folder.SearchAsync(SearchQuery.HeaderContains("Message-Id", item.MessageId.Trim('<', '>')), cancellationToken);
        if (candidates.Count == 0) return null;
        var summaries = await folder.FetchAsync(candidates, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate | MessageSummaryItems.Size | MessageSummaryItems.Envelope, cancellationToken);
        foreach (var summary in summaries)
        {
            var candidate = Fingerprint(summary.Envelope?.MessageId, summary.InternalDate, summary.Size ?? 0, summary.Envelope?.Subject, summary.Envelope?.From?.ToString());
            if (string.Equals(candidate, item.Fingerprint, StringComparison.Ordinal)) return summary.UniqueId;
        }
        return null;
    }

    private static async Task<IMailFolder> GetOrCreateFolderAsync(ImapClient client, string path, CancellationToken cancellationToken)
    {
        try { return await client.GetFolderAsync(path, cancellationToken); }
        catch (FolderNotFoundException)
        {
            var current = client.GetFolder(client.PersonalNamespaces[0]) ?? throw new InvalidOperationException("Hedef kişisel klasör kökü bulunamadı.");
            foreach (var segment in path.Split(new[] { '/', current.DirectorySeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { current = await client.GetFolderAsync(current.FullName.Length == 0 ? segment : current.FullName + current.DirectorySeparator + segment, cancellationToken) ?? throw new FolderNotFoundException(segment); }
                catch (FolderNotFoundException) { current = await current.CreateAsync(segment, true, cancellationToken) ?? throw new InvalidOperationException($"Hedef klasör oluşturulamadı: {segment}"); }
            }
            return current;
        }
    }

    private static async Task ExecuteWithRetryAsync(Func<Task> action, int maximumRetries, CancellationToken cancellationToken)
    {
        var delays = new[] { 5, 15, 30, 60, 120, 300 };
        for (var attempt = 1; ; attempt++)
        {
            try { await action(); return; }
            catch (Exception ex) when (attempt < maximumRetries && IsTransient(ex))
            {
                var seconds = delays[Math.Min(attempt - 1, delays.Length - 1)] + Random.Shared.NextDouble() * 2;
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex is IOException or TimeoutException or ServiceNotConnectedException || ex.Message.Contains("tempor", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("too many", StringComparison.OrdinalIgnoreCase);
    private static string Fingerprint(string? messageId, DateTimeOffset? date, long size, string? subject, string? from) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{messageId?.Trim().ToLowerInvariant()}|{date:O}|{size}|{subject?.Trim()}|{from?.Trim().ToLowerInvariant()}")));
    private const MessageFlags SettableFlags = MessageFlags.Seen | MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Draft;
}
