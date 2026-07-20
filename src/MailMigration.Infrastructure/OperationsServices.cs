using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MailMigration.Application;
using MailMigration.Domain;
using Renci.SshNet;

namespace MailMigration.Infrastructure;

public sealed class SshService : ISshService
{
    public async Task<ConnectionTestResult> TestAsync(string host, int port, string username, string password, string? expectedFingerprint, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            return await Task.Run(() =>
            {
                using var client = new SshClient(host, port, username, password);
                string? fingerprint = null;
                client.HostKeyReceived += (_, e) => { fingerprint = Convert.ToHexString(e.FingerPrint); e.CanTrust = expectedFingerprint is null || string.Equals(expectedFingerprint.Replace(":", ""), fingerprint, StringComparison.OrdinalIgnoreCase); };
                client.Connect();
                var response = client.RunCommand("uname -srm").Result.Trim();
                client.Disconnect();
                return new ConnectionTestResult(ConnectionTestKind.Ssh, true, timer.Elapsed, response, Certificate: fingerprint, AuthenticationMethod: "SSH password");
            }, cancellationToken);
        }
        catch (Exception ex) { return new(ConnectionTestKind.Ssh, false, timer.Elapsed, string.Empty, Error: $"{ex.GetType().Name}: {ex.Message}"); }
    }
}

public sealed class DirectAdminService(HttpClient client) : IDirectAdminService
{
    public async Task<ConnectionTestResult> TestApiAsync(Uri endpoint, string username, string loginKey, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "/api/version"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{loginKey}")));
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new(ConnectionTestKind.DirectAdminApi, response.IsSuccessStatusCode, timer.Elapsed, content, Error: response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) { return new(ConnectionTestKind.DirectAdminApi, false, timer.Elapsed, string.Empty, Error: $"{ex.GetType().Name}: {ex.Message}"); }
    }
}

public sealed class MigrationVerificationService(IMigrationStore store) : IMigrationVerificationService
{
    public async Task<VerificationResult> VerifyAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var messages = await store.GetMessagesAsync(jobId, cancellationToken);
        var completed = messages.LongCount(x => x.Status == MessageMigrationStatus.Completed);
        var failed = messages.LongCount(x => x.Status == MessageMigrationStatus.Failed);
        var problems = messages.Where(x => x.Status == MessageMigrationStatus.Failed).Take(100).Select(x => $"{x.SourceFolderPath}/{x.SourceUid}: {x.LastError}").ToList();
        return new(failed == 0 && messages.Count > 0, messages.Count, completed, failed, problems);
    }
}

public sealed class ReportService : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.Default };
    public async Task<IReadOnlyList<string>> GenerateAsync(MigrationReport report, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var stem = $"migration-{report.Job.Id:N}";
        var jsonPath = Path.Combine(outputDirectory, stem + ".json");
        var csvPath = Path.Combine(outputDirectory, stem + ".csv");
        var htmlPath = Path.Combine(outputDirectory, stem + ".html");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(csvPath, "job_id,status,completed,skipped,failed,bytes\n" + $"{report.Job.Id},{report.Job.Status},{report.Completed},{report.Skipped},{report.Failed},{report.Bytes}\n", cancellationToken);
        var html = $"<!doctype html><meta charset=\"utf-8\"><title>Mail Migration Report</title><h1>Taşıma raporu</h1><dl><dt>İş</dt><dd>{report.Job.Id}</dd><dt>Durum</dt><dd>{report.Job.Status}</dd><dt>Aktarılan</dt><dd>{report.Completed}</dd><dt>Atlanan</dt><dd>{report.Skipped}</dd><dt>Başarısız</dt><dd>{report.Failed}</dd><dt>Boyut</dt><dd>{report.Bytes:N0} bayt</dd></dl>";
        await File.WriteAllTextAsync(htmlPath, html, cancellationToken);
        return new[] { htmlPath, csvPath, jsonPath };
    }
}
