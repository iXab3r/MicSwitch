using PoeShared.Scaffolding;

namespace MicSwitch.MainWindow.Models
{
    internal interface IMicrophoneController : IDisposableReactiveObject
    {
        MicrophoneLineData LineId { get; set; }
        
        bool? Mute { get; set; }
        
        double? VolumePercent { get; set; }
    }
}