using System.Windows;
using PoeShared.Modularity;
using PoeShared.Native;

namespace MicSwitch
{
    internal sealed class MicSwitchConfig : IPoeEyeConfigVersioned, IOverlayConfig
    {
        public bool IsPushToTalkMode { get; set; }
        
        public MicrophoneLineData MicrophoneLineId { get; set; }

        public string MicrophoneHotkey { get; set; }
        
        public string MicrophoneHotkeyAlt { get; set; }

        public TwoStateNotification Notification { get; set; } = new TwoStateNotification
        {
            On = "Beep300",
            Off = "Beep750"
        };
        
        public Point OverlayLocation { get; set; }
        
        public Size OverlaySize { get; set; }
        
        public float OverlayOpacity { get; set; }
        
        public bool IsVisible { get; set; } = true;

        public double ScaleFactor { get; set; } = 1;
        
        public int Version { get; set; } = 1;
    }
}