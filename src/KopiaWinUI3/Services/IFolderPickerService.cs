using Microsoft.UI.Xaml;

namespace KopiaWinUI3.Services;

public interface IFolderPickerService
{
    void Initialize(Window window);

    Task<string?> PickFolderAsync();
}
