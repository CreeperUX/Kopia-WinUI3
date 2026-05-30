using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using KopiaWinUI3.ViewModels;

namespace KopiaWinUI3;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        ViewModel.ServerStarted += OnServerStarted;
        Closed += OnClosed;

        _ = ViewModel.InitializeAsync();
    }

    private void OnServerStarted(object? sender, Uri serverUri)
    {
        KopiaWebView.Source = serverUri;
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.ServerStarted -= OnServerStarted;
        await ViewModel.StopAsync();
    }
}
