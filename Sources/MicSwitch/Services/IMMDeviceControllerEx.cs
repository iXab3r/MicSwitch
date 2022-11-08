using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMMDeviceControllerEx : IMMDeviceController
    {
        new MMDeviceId LineId { get; set; }
        
        bool EnableVolumeControl { get; set; }
    }
}