using PoeShared.Scaffolding;

namespace MicSwitch.Services
{
    internal interface IMicrophoneController : IDisposableReactiveObject
    {
        MicrophoneLineData LineId { get; }

        bool? Mute { get; set; }

        double? VolumePercent { get; set; }
    }
}