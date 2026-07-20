namespace MailMigration.Domain;

public enum ConnectionSecurity { None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable }
public enum MigrationStatus { Draft, Ready, Running, Paused, Verifying, Completed, PartiallyCompleted, Failed, Cancelled }
public enum MessageMigrationStatus { Pending, Downloading, Downloaded, Uploading, Completed, Skipped, Failed, RetryWaiting, Cancelled }
public enum ConnectionTestKind { Dns, ImapConnection, ImapAuthentication, SmtpConnection, SmtpAuthentication, MailboxWrite, FolderCreate, Ssh, DirectAdminApi }

public sealed class ServerConnectionSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public ConnectionSecurity Security { get; set; } = ConnectionSecurity.SslOnConnect;
    public bool AcceptInvalidCertificate { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class MailAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string CredentialKey { get; set; } = string.Empty;
    public ServerConnectionSettings Imap { get; set; } = new();
    public ServerConnectionSettings? Smtp { get; set; }
}

public sealed class MigrationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceAccountId { get; set; }
    public Guid TargetAccountId { get; set; }
    public MigrationStatus Status { get; set; } = MigrationStatus.Draft;
    public int BatchSize { get; set; } = 100;
    public int Parallelism { get; set; } = 3;
    public int MaximumRetries { get; set; } = 10;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class MigrationFolder
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public uint SourceUidValidity { get; set; }
    public long TotalMessages { get; set; }
    public long CompletedMessages { get; set; }
}

public sealed class MigrationMessage
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public string SourceFolderPath { get; set; } = string.Empty;
    public string TargetFolderPath { get; set; } = string.Empty;
    public uint SourceUid { get; set; }
    public uint SourceUidValidity { get; set; }
    public uint? TargetUid { get; set; }
    public string? MessageId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTimeOffset? MessageDate { get; set; }
    public MessageMigrationStatus Status { get; set; } = MessageMigrationStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class MigrationCheckpoint
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public uint UidValidity { get; set; }
    public uint LastCompletedUid { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record ConnectionTestResult(ConnectionTestKind Kind, bool Success, TimeSpan Duration, string ServerResponse, string? TlsVersion = null, string? Certificate = null, string? AuthenticationMethod = null, string? Error = null, bool Critical = true);
public sealed record FolderMapping(string SourcePath, string TargetPath, string? SpecialUse = null, bool UserDefined = false);
public sealed record MailboxFolderInfo(string FullName, long MessageCount, long EstimatedBytes, uint UidValidity, string? SpecialUse);
public sealed record MailboxAnalysis(IReadOnlyList<MailboxFolderInfo> Folders, long TotalMessages, long EstimatedBytes, DateTimeOffset? OldestMessage, DateTimeOffset? NewestMessage);
public sealed record MigrationProgress(Guid JobId, string Folder, long Processed, long Total, long Completed, long Skipped, long Failed, long Bytes, double MessagesPerSecond);
public sealed record VerificationResult(bool Success, long SourceCount, long CompletedCount, long FailedCount, IReadOnlyList<string> Problems);
public sealed record MigrationReport(MigrationJob Job, long Completed, long Skipped, long Failed, long Bytes, VerificationResult Verification);
