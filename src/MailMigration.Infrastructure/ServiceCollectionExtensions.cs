using MailMigration.Application;
using Microsoft.Extensions.DependencyInjection;

namespace MailMigration.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICredentialStorageService, CredentialStorageService>();
        services.AddSingleton<IConnectionTestService, ConnectionTestService>();
        services.AddSingleton<IMailboxAnalysisService, MailboxAnalysisService>();
        services.AddSingleton<IFolderMappingService, FolderMappingService>();
        services.AddSingleton<IPauseController, PauseController>();
        services.AddSingleton<IMigrationVerificationService, MigrationVerificationService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<ISshService, SshService>();
        services.AddHttpClient<IDirectAdminService, DirectAdminService>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IMessageMigrationService, MessageMigrationService>();
        return services;
    }
}
