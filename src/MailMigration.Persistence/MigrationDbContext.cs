using MailMigration.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailMigration.Persistence;

public sealed class MigrationDbContext(DbContextOptions<MigrationDbContext> options) : DbContext(options)
{
    public DbSet<MigrationJob> Jobs => Set<MigrationJob>();
    public DbSet<MigrationFolder> Folders => Set<MigrationFolder>();
    public DbSet<MigrationMessage> Messages => Set<MigrationMessage>();
    public DbSet<MigrationCheckpoint> Checkpoints => Set<MigrationCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationJob>().HasKey(x => x.Id);
        modelBuilder.Entity<MigrationMessage>().HasIndex(x => new { x.JobId, x.SourceFolderPath, x.SourceUidValidity, x.SourceUid }).IsUnique();
        modelBuilder.Entity<MigrationMessage>().HasIndex(x => new { x.JobId, x.Fingerprint });
        modelBuilder.Entity<MigrationCheckpoint>().HasIndex(x => new { x.JobId, x.FolderPath }).IsUnique();
        modelBuilder.Entity<MigrationFolder>().HasIndex(x => new { x.JobId, x.SourcePath }).IsUnique();
        modelBuilder.Entity<MigrationMessage>().Property(x => x.SourceUid).HasConversion<long>();
        modelBuilder.Entity<MigrationMessage>().Property(x => x.SourceUidValidity).HasConversion<long>();
        modelBuilder.Entity<MigrationMessage>().Property(x => x.TargetUid).HasConversion<long?>();
        modelBuilder.Entity<MigrationCheckpoint>().Property(x => x.UidValidity).HasConversion<long>();
        modelBuilder.Entity<MigrationCheckpoint>().Property(x => x.LastCompletedUid).HasConversion<long>();
        modelBuilder.Entity<MigrationFolder>().Property(x => x.SourceUidValidity).HasConversion<long>();
    }
}
