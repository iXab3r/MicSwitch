using MicSwitch.MainWindow.Models;
using PoeShared.Native;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicSwitchOverlayViewModel : IOverlayViewModel
    {
        bool Mute { get; }
        
        bool IsEnabled { get; set; }
        
        OverlayVisibilityMode OverlayVisibilityMode { get; set; }
    }
}