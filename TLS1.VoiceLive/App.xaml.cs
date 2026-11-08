using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TLS1.VoiceLive;

public partial class App : System.Windows.Application
{
    private readonly IHost? _host;
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public App()
    {
        try
        {
            // Log lỗi ra file trong thư mục publish
            var logDir = AppDomain.CurrentDomain.BaseDirectory;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(System.IO.Path.Combine(logDir, "error.log"))
                .WriteTo.Console()
                .CreateLogger();

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.Configure<TLS1.Core.Configuration.TtsOptions>(
                        context.Configuration.GetSection("TTS"));
                    services.Configure<TLS1.Core.Configuration.TwitchOptions>(
                        context.Configuration.GetSection("Platforms:Twitch"));
                    services.Configure<TLS1.Core.Configuration.YoutubeOptions>(
                        context.Configuration.GetSection("Platforms:YouTube"));

                    services.AddSingleton<TLS1.Core.Interfaces.IConfigurationService,
                        TLS1.Infrastructure.Services.ConfigurationService>();
                    services.AddSingleton<TLS1.Core.Interfaces.AI.ITtsEngine,
                        TLS1.AI.Engines.EdgeTtsEngine>();

                    // Chỉ đăng ký MockChatProvider để tránh crash từ Twitch/Youtube
                    services.AddSingleton<TLS1.Core.Interfaces.Chat.IChatProvider,
                        TLS1.Chat.Providers.MockChatProvider>();

                    services.AddSingleton<TLS1.Core.Interfaces.Chat.IChatAggregator,
                        TLS1.Application.Services.ChatAggregator>();
                    services.AddSingleton<ViewModels.MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
            ServiceProvider = _host.Services;
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                ex.ToString());
            MessageBox.Show($"Lỗi khởi động:\n{ex.Message}",
                "TLS1 Voice Live", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (_host is null)
        {
            Shutdown();
            return;
        }
        try
        {
            await _host.StartAsync();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                ex.ToString());
            MessageBox.Show($"Lỗi khởi động:\n{ex.Message}",
                "TLS1 Voice Live", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
            using (_host) { await _host.StopAsync(TimeSpan.FromSeconds(5)); }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}