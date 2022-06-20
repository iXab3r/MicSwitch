using MicSwitch.MainWindow.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicSwitchOverlayViewModel : IOverlayViewModel
    {
        bool Mute { get; }
        
        bool IsEnabled { get; set; }
        
        OverlayVisibilityMode OverlayVisibilityMode { get; set; }
    }
}