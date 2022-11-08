using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMMDeviceController : IDisposableReactiveObject
    {
        MMDeviceId LineId { get; }

        bool? Mute { get; set; }

        double? VolumePercent { get; set; }
    }
}