using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using KopiaWinUI3.Services;
using KopiaWinUI3.ViewModels;

namespace KopiaWinUI3;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        App.Services.GetRequiredService<IFolderPickerService>().Initialize(this);
        _ = ViewModel.InitializeAsync();
    }
}
