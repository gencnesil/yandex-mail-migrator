using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using MailMigration.Application;
using MailMigration.Domain;

namespace MailMigration.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICredentialStorageService credentials; private readonly IConnectionTestService tests; private readonly IMailboxAnalysisService analyzer;
    private readonly IFolderMappingService mapper; private readonly IMessageMigrationService migrator; private readonly IMigrationStore store;
    private readonly IMigrationVerificationService verifier; private readonly IReportService reports; private readonly IPauseController pause;
    private CancellationTokenSource? operation; private bool connectionsReady; private string status = "Bağlantı bilgilerini girerek başlayın.";
    private double progressPercent; private long processed, total, completed, skipped, failed, transferredBytes;

    public MainViewModel(ICredentialStorageService credentials, IConnectionTestService tests, IMailboxAnalysisService analyzer, IFolderMappingService mapper, IMessageMigrationService migrator, IMigrationStore store, IMigrationVerificationService verifier, IReportService reports, IPauseController pause)
    {
        this.credentials = credentials; this.tests = tests; this.analyzer = analyzer; this.mapper = mapper; this.migrator = migrator; this.store = store; this.verifier = verifier; this.reports = reports; this.pause = pause;
        TestCommand = new(TestConnectionsAsync); AnalyzeCommand = new(AnalyzeAsync, () => connectionsReady); StartCommand = new(StartAsync, () => connectionsReady && Mappings.Count > 0);
        PauseCommand = new(() => { pause.Pause(); Status = "Taşıma duraklatıldı."; return Task.CompletedTask; });
        ResumeCommand = new(() => { pause.Resume(); Status = "Taşıma devam ediyor."; return Task.CompletedTask; });
        CancelCommand = new(() => { operation?.Cancel(); return Task.CompletedTask; });
    }

    public string SourceHost { get; set; } = string.Empty; public int SourcePort { get; set; } = 993; public string SourceEmail { get; set; } = string.Empty; public string SourceUsername { get; set; } = string.Empty;
    public string TargetHost { get; set; } = string.Empty; public int TargetPort { get; set; } = 993; public string TargetEmail { get; set; } = string.Empty; public string TargetUsername { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100; public int Parallelism { get; set; } = 3;
    public string SourcePassword { private get; set; } = string.Empty; public string TargetPassword { private get; set; } = string.Empty;
    public ObservableCollection<ConnectionTestResult> ConnectionResults { get; } = []; public ObservableCollection<FolderMapping> Mappings { get; } = [];
    public AsyncRelayCommand TestCommand { get; } public AsyncRelayCommand AnalyzeCommand { get; } public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand PauseCommand { get; } public AsyncRelayCommand ResumeCommand { get; } public AsyncRelayCommand CancelCommand { get; }
    public string Status { get => status; private set => Set(ref status, value); } public double ProgressPercent { get => progressPercent; private set => Set(ref progressPercent, value); }
    public long Processed { get => processed; private set => Set(ref processed, value); } public long Total { get => total; private set => Set(ref total, value); }
    public long Completed { get => completed; private set => Set(ref completed, value); } public long Skipped { get => skipped; private set => Set(ref skipped, value); }
    public long Failed { get => failed; private set => Set(ref failed, value); } public long TransferredBytes { get => transferredBytes; private set => Set(ref transferredBytes, value); }

    private MailAccount Source => Account("source", SourceHost, SourcePort, SourceEmail, SourceUsername);
    private MailAccount Target => Account("target", TargetHost, TargetPort, TargetEmail, TargetUsername);
    private static MailAccount Account(string key, string host, int port, string email, string username) => new() { Id = StableGuid(key + email), Email = email, Username = username, CredentialKey = key + ":" + email, Imap = new() { Host = host, Port = port, Security = ConnectionSecurity.SslOnConnect } };

    private async Task TestConnectionsAsync()
    {
        operation = new(); ConnectionResults.Clear(); connectionsReady = false; Status = "Bağlantılar test ediliyor...";
        try
        {
            var source = Source; var target = Target;
            await credentials.SaveAsync(source.CredentialKey, SourcePassword, operation.Token); await credentials.SaveAsync(target.CredentialKey, TargetPassword, operation.Token);
            var all = (await Task.WhenAll(tests.TestImapAsync(source, operation.Token), tests.TestImapAsync(target, operation.Token))).SelectMany(x => x).ToList();
            foreach (var result in all) ConnectionResults.Add(result);
            connectionsReady = all.Where(x => x.Critical).All(x => x.Success);
            Status = connectionsReady ? "Kritik IMAP testleri başarılı. Analize geçebilirsiniz." : "Kritik bağlantı testi başarısız; sunucu, port, TLS ve kimlik bilgilerini kontrol edin.";
        }
        catch (Exception ex) { Status = "Bağlantı testi tamamlanamadı: " + ex.Message; }
        finally { AnalyzeCommand.RaiseCanExecuteChanged(); StartCommand.RaiseCanExecuteChanged(); }
    }

    private async Task AnalyzeAsync()
    {
        operation = new(); Status = "Kaynak ve hedef klasörler analiz ediliyor...";
        try
        {
            var analyses = await Task.WhenAll(analyzer.AnalyzeAsync(Source, operation.Token), analyzer.AnalyzeAsync(Target, operation.Token));
            Mappings.Clear(); foreach (var mapping in mapper.CreateMappings(analyses[0], analyses[1])) Mappings.Add(mapping);
            Total = analyses[0].TotalMessages; Status = $"{analyses[0].Folders.Count} klasör, {Total:N0} mesaj, yaklaşık {analyses[0].EstimatedBytes / 1048576d:N1} MB bulundu.";
        }
        catch (Exception ex) { Status = "Analiz başarısız: " + ex.Message; }
        finally { StartCommand.RaiseCanExecuteChanged(); }
    }

    private async Task StartAsync()
    {
        operation = new(); var source = Source; var target = Target;
        var job = new MigrationJob { SourceAccountId = source.Id, TargetAccountId = target.Id, BatchSize = Math.Clamp(BatchSize, 10, 1000), Parallelism = Math.Clamp(Parallelism, 1, 10), Status = MigrationStatus.Ready };
        var progress = new Progress<MigrationProgress>(p => { Processed = p.Processed; Total = p.Total; Completed = p.Completed; Skipped = p.Skipped; Failed = p.Failed; TransferredBytes = p.Bytes; ProgressPercent = p.Total == 0 ? 0 : p.Processed * 100d / p.Total; Status = $"{p.Folder} — {p.MessagesPerSecond:N1} mesaj/sn"; });
        try
        {
            await store.SaveJobAsync(job, operation.Token); await migrator.RunAsync(job, source, target, Mappings.ToList(), progress, operation.Token);
            var verification = await verifier.VerifyAsync(job.Id, operation.Token); job.Status = verification.Success ? MigrationStatus.Completed : MigrationStatus.PartiallyCompleted; job.CompletedAt = DateTimeOffset.UtcNow; await store.SaveJobAsync(job, operation.Token);
            var messages = await store.GetMessagesAsync(job.Id, operation.Token); var report = new MigrationReport(job, messages.LongCount(x => x.Status == MessageMigrationStatus.Completed), messages.LongCount(x => x.Status == MessageMigrationStatus.Skipped), messages.LongCount(x => x.Status == MessageMigrationStatus.Failed), messages.Where(x => x.Status == MessageMigrationStatus.Completed).Sum(x => x.Size), verification);
            var paths = await reports.GenerateAsync(report, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mail Migration Reports"), operation.Token); Status = $"Taşıma {job.Status}. Rapor: {paths[0]}";
        }
        catch (OperationCanceledException) { Status = "Taşıma güvenli şekilde iptal edildi; checkpoint korundu."; }
        catch (Exception ex) { Status = "Taşıma hatası: " + ex.Message; }
    }

    private static Guid StableGuid(string value) => new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
}
