using System.Windows;
using PoeShared.Modularity;
using PoeShared.Native;

namespace MicSwitch
{
    internal sealed class MicSwitchConfig : IPoeEyeConfigVersioned, IOverlayConfig
    {
        public bool IsPushToTalkMode { get; set; }
        
        public MicrophoneLineData MicrophoneLineId { get; set; }
        
        public int Version { get; set; } = 1;
        
        public Point OverlayLocation { get; set; }
        
        public Size OverlaySize { get; set; }
        
        public float OverlayOpacity { get; set; }
        
        public bool IsVisible { get; set; } = true;

        public double ScaleFactor { get; set; } = 1;
    }
}