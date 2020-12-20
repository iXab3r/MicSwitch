using System.Windows;
using MicSwitch.MainWindow.Models;
using MicSwitch.Services;
using PoeShared.Modularity;
using PoeShared.Native;

namespace MicSwitch.Modularity
{
    internal sealed class MicSwitchConfig : IPoeEyeConfigVersioned, IOverlayConfig
    {
        public static readonly string DiscordInviteLink = @"https://discord.gg/pFHHebM";
        
        public bool? IsPushToTalkMode { get; set; }

        public MuteMode MuteMode { get; set; } = MuteMode.ToggleMute;
        
        public bool StartMinimized { get; set; }

        public MicrophoneLineData MicrophoneLineId { get; set; }

        public bool SuppressHotkey { get; set; } = true;
        
        public bool MinimizeOnClose { get; set; } = true;
        
        public bool VolumeControlEnabled { get; set; } = false;
        
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

        public bool OverlayEnabled { get; set; } = true;
        
        public byte[] MutedMicrophoneIcon { get; set; }
        
        public byte[] MicrophoneIcon { get; set; }

        public int Version { get; set; } = 1;
    }
}