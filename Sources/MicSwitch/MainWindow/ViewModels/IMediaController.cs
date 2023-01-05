using MicSwitch.Services;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels;

internal interface IMediaController : IDisposableReactiveObject
{
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// If true, setting Volume will propagate this change to Controller, otherwise Volume will be in read-only mode
    /// </summary>
    bool VolumeControlIsEnabled { get; set; }
    
    IMMDeviceControllerEx Controller { get; }
    
    IReadOnlyObservableCollection<MMDeviceId> Devices { get; }
    
    MMDeviceId DeviceId { get; set; }
    
    bool? Mute { get; }
        
    float? Volume { get; set; }
    
    CommandWrapper MuteCommand { get; }
}