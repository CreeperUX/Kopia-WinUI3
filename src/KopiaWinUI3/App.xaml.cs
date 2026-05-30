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
        Services = ConfigureServices();
        InitializeComponent();
    }

    public static IServiceProvider Services { get; private set; } = default!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow
        {
            Title = "Kopia WinUI3"
        };
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IKopiaLocator, KopiaLocator>();
        services.AddSingleton<ILocalPortService, LocalPortService>();
        services.AddSingleton<IKopiaProcessService, KopiaProcessService>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
