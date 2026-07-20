using MailMigration.Domain;
using MailMigration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MailMigration.Tests;

public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"mailmigration-test-{Guid.NewGuid():N}.db");
    private MigrationStore store = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MigrationDbContext>().UseSqlite($"Data Source={database};Pooling=False").Options;
        store = new MigrationStore(new TestFactory(options));
        await store.InitializeAsync();
    }
    public Task DisposeAsync() { if (File.Exists(database)) File.Delete(database); return Task.CompletedTask; }

    [Fact]
    public async Task Completed_uid_is_detected_after_restart_style_read()
    {
        var job = new MigrationJob(); await store.SaveJobAsync(job, default);
        await store.SaveMessageAsync(new() { JobId = job.Id, SourceFolderPath = "INBOX", TargetFolderPath = "INBOX", SourceUid = 42, SourceUidValidity = 7, Fingerprint = "ABC", Status = MessageMigrationStatus.Completed }, default);
        Assert.True(await store.IsCompletedAsync(job.Id, "INBOX", 7, 42, "different", default));
    }

    [Fact]
    public async Task Message_upsert_does_not_create_duplicates()
    {
        var job = new MigrationJob(); await store.SaveJobAsync(job, default);
        var message = new MigrationMessage { JobId = job.Id, SourceFolderPath = "INBOX", TargetFolderPath = "INBOX", SourceUid = 9, SourceUidValidity = 2, Fingerprint = "FP" };
        await store.SaveMessageAsync(message, default); message.Status = MessageMigrationStatus.Completed; await store.SaveMessageAsync(message, default);
        Assert.Single(await store.GetMessagesAsync(job.Id, default));
    }

    private sealed class TestFactory(DbContextOptions<MigrationDbContext> options) : IDbContextFactory<MigrationDbContext>
    {
        public MigrationDbContext CreateDbContext() => new(options);
        public Task<MigrationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
