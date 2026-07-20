using MailMigration.Application;
using MailMigration.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailMigration.Persistence;

public sealed class MigrationStore(IDbContextFactory<MigrationDbContext> factory) : IMigrationStore
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
    }

    public async Task SaveJobAsync(MigrationJob job, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Jobs.AnyAsync(x => x.Id == job.Id, cancellationToken);
        db.Entry(job).State = exists ? EntityState.Modified : EntityState.Added;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MigrationJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Jobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<MigrationJob>> GetIncompleteJobsAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Jobs.AsNoTracking().Where(x => x.Status != MigrationStatus.Completed && x.Status != MigrationStatus.Cancelled).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<bool> IsCompletedAsync(Guid jobId, string folder, uint uidValidity, uint uid, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Messages.AsNoTracking().AnyAsync(x => x.JobId == jobId && (x.Status == MessageMigrationStatus.Completed || x.Status == MessageMigrationStatus.Skipped) &&
            ((x.SourceFolderPath == folder && x.SourceUidValidity == uidValidity && x.SourceUid == uid) || x.Fingerprint == fingerprint), cancellationToken);
    }

    public async Task SaveMessageAsync(MigrationMessage message, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var current = await db.Messages.SingleOrDefaultAsync(x => x.JobId == message.JobId && x.SourceFolderPath == message.SourceFolderPath && x.SourceUidValidity == message.SourceUidValidity && x.SourceUid == message.SourceUid, cancellationToken);
        if (current is null) db.Messages.Add(message);
        else db.Entry(current).CurrentValues.SetValues(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveCheckpointAsync(MigrationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var current = await db.Checkpoints.SingleOrDefaultAsync(x => x.JobId == checkpoint.JobId && x.FolderPath == checkpoint.FolderPath, cancellationToken);
        if (current is null) db.Checkpoints.Add(checkpoint);
        else { current.LastCompletedUid = checkpoint.LastCompletedUid; current.UidValidity = checkpoint.UidValidity; current.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MigrationMessage>> GetMessagesAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Messages.AsNoTracking().Where(x => x.JobId == jobId).ToListAsync(cancellationToken);
    }
}
