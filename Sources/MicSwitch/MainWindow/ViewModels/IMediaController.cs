using MicSwitch.Services;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels;

internal interface IMediaController : IDisposableReactiveObject
{
    bool IsEnabled { get; set; }
    
    IMMDeviceControllerEx Controller { get; }
    
    IReadOnlyObservableCollection<MMDeviceId> Devices { get; }
    
    MMDeviceId DeviceId { get; set; }
    
    bool Mute { get; }
        
    double Volume { get; set; }
    
    CommandWrapper MuteMicrophoneCommand { get; }
}