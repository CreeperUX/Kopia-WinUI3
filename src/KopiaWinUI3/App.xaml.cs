using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using KopiaWinUI3.Services;
using KopiaWinUI3.ViewModels;

namespace KopiaWinUI3;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += OnUnhandledException;
        try
        {
            Services = ConfigureServices();
            InitializeComponent();
        }
        catch (Exception ex)
        {
            WriteStartupError(ex);
            throw;
        }
    }

    public static IServiceProvider Services { get; private set; } = default!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow
            {
                Title = "Kopia WinUI3"
            };
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteStartupError(ex);
            throw;
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IKopiaLocator, KopiaLocator>();
        services.AddSingleton<IKopiaCommandService, KopiaCommandService>();
        services.AddSingleton<ILocalPortService, LocalPortService>();
        services.AddSingleton<IKopiaProcessService, KopiaProcessService>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteStartupError(e.Exception);
    }

    private static void WriteStartupError(Exception exception)
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        File.WriteAllText(Path.Combine(logDirectory, "startup-error.txt"), exception.ToString());
    }
}
