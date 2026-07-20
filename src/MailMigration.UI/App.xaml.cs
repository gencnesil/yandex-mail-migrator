using System.IO;
using System.Windows;
using MailMigration.Application;
using MailMigration.Infrastructure;
using MailMigration.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MailMigration.UI;

public partial class App : System.Windows.Application
{
    private IHost? host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MailMigrationDesktop");
        Directory.CreateDirectory(dataRoot);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(dataRoot, "logs", "application-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Beklenmeyen UI hatası");
            MessageBox.Show($"Uygulama hatası:\n\n{args.Exception.Message}\n\nDetaylar günlük dosyasına kaydedildi.", "Mail Migration Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            host = Host.CreateDefaultBuilder().UseSerilog().ConfigureServices(services =>
            {
                services.AddMigrationPersistence(Path.Combine(dataRoot, "migration.db"));
                services.AddMigrationInfrastructure();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            }).Build();
            await host.StartAsync();
            await host.Services.GetRequiredService<IMigrationStore>().InitializeAsync();
            host.Services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Uygulama başlatılamadı");
            await Log.CloseAndFlushAsync();
            MessageBox.Show($"Uygulama başlatılamadı:\n\n{ex.Message}\n\nGünlük: {Path.Combine(dataRoot, "logs")}", "Mail Migration Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null) { await host.StopAsync(TimeSpan.FromSeconds(10)); host.Dispose(); }
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
