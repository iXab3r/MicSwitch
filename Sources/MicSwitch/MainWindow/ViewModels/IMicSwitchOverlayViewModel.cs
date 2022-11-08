using MicSwitch.MainWindow.Models;
using MicSwitch.Services;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicSwitchOverlayViewModel : IOverlayViewModel
    {
        bool MicrophoneMute { get; }
        
        double? OutputVolume { get; }
        
        bool IsEnabled { get; set; }
        
        OverlayVisibilityMode OverlayVisibilityMode { get; set; }
        
        IMMDeviceController MicrophoneDeviceController { get; set; }
        
        IMMDeviceController OutputDeviceController { get; set; }
    }
}