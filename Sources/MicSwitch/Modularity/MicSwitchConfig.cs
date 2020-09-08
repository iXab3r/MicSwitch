using System.Windows;
using MicSwitch.MainWindow.Models;
using MicSwitch.Services;
using PoeShared.Modularity;
using PoeShared.Native;

namespace MicSwitch.Modularity
{
    internal sealed class MicSwitchConfig : IPoeEyeConfigVersioned, IOverlayConfig
    {
        public bool IsPushToTalkMode { get; set; }
        
        public bool StartMinimized { get; set; }

        public MicrophoneLineData MicrophoneLineId { get; set; }

        public bool SuppressHotkey { get; set; } = true;
        
        public string MicrophoneHotkey { get; set; }

        public string MicrophoneHotkeyAlt { get; set; }

        public TwoStateNotification Notification { get; set; } = new TwoStateNotification
        {
            On = "Beep300",
            Off = "Beep750"
        };

        public bool IsVisible { get; set; } = true;

        public Point OverlayLocation { get; set; }

        public Size OverlaySize { get; set; }

        public float OverlayOpacity { get; set; }
        
        public byte[] MutedMicrophoneIcon { get; set; }
        
        public byte[] MicrophoneIcon { get; set; }

        public int Version { get; set; } = 1;
    }
}