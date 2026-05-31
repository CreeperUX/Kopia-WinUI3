using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RcloneWinUI3.Services;
using RcloneWinUI3.ViewModels;
using WinRT.Interop;

namespace RcloneWinUI3;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        SetWindowIcon();

        App.Services.GetRequiredService<IFolderPickerService>().Initialize(this);
        App.Services.GetRequiredService<INotificationDialogService>().Initialize(this);
        _ = ViewModel.InitializeAsync();
    }

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow.GetFromWindowId(windowId).SetIcon(iconPath);
    }
}
