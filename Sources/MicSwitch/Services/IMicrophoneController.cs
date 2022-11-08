using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMicrophoneController : IDisposableReactiveObject
    {
        MMDeviceLineData LineId { get; }

        bool? Mute { get; set; }

        double? VolumePercent { get; set; }
    }
}