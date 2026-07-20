using MailMigration.Domain;

namespace MailMigration.Application;

public interface ICredentialStorageService
{
    Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public interface IConnectionTestService
{
    Task<IReadOnlyList<ConnectionTestResult>> TestImapAsync(MailAccount account, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectionTestResult>> TestSmtpAsync(MailAccount account, CancellationToken cancellationToken);
}

public interface IMailboxAnalysisService
{
    Task<MailboxAnalysis> AnalyzeAsync(MailAccount account, CancellationToken cancellationToken);
}

public interface IFolderMappingService
{
    IReadOnlyList<FolderMapping> CreateMappings(MailboxAnalysis source, MailboxAnalysis target);
}

public interface IMigrationStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveJobAsync(MigrationJob job, CancellationToken cancellationToken);
    Task<MigrationJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MigrationJob>> GetIncompleteJobsAsync(CancellationToken cancellationToken);
    Task<bool> IsCompletedAsync(Guid jobId, string folder, uint uidValidity, uint uid, string fingerprint, CancellationToken cancellationToken);
    Task SaveMessageAsync(MigrationMessage message, CancellationToken cancellationToken);
    Task SaveCheckpointAsync(MigrationCheckpoint checkpoint, CancellationToken cancellationToken);
    Task<IReadOnlyList<MigrationMessage>> GetMessagesAsync(Guid jobId, CancellationToken cancellationToken);
}

public interface IMessageMigrationService
{
    Task RunAsync(MigrationJob job, MailAccount source, MailAccount target, IReadOnlyList<FolderMapping> mappings, IProgress<MigrationProgress>? progress, CancellationToken cancellationToken);
}

public interface IMigrationVerificationService
{
    Task<VerificationResult> VerifyAsync(Guid jobId, CancellationToken cancellationToken);
}

public interface IReportService
{
    Task<IReadOnlyList<string>> GenerateAsync(MigrationReport report, string outputDirectory, CancellationToken cancellationToken);
}

public interface ISshService
{
    Task<ConnectionTestResult> TestAsync(string host, int port, string username, string password, string? expectedFingerprint, CancellationToken cancellationToken);
}

public interface IDirectAdminService
{
    Task<ConnectionTestResult> TestApiAsync(Uri endpoint, string username, string loginKey, CancellationToken cancellationToken);
}

public interface IPauseController
{
    bool IsPaused { get; }
    void Pause();
    void Resume();
    Task WaitIfPausedAsync(CancellationToken cancellationToken);
}
