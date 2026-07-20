using MailMigration.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MailMigration.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationPersistence(this IServiceCollection services, string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
        services.AddDbContextFactory<MigrationDbContext>(options => options.UseSqlite($"Data Source={databasePath};Cache=Shared"));
        services.AddSingleton<IMigrationStore, MigrationStore>();
        return services;
    }
}
