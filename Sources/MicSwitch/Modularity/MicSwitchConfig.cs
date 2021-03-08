using System;
using System.Drawing;
using MicSwitch.MainWindow.Models;
using MicSwitch.Services;
using PoeShared.Modularity;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace MicSwitch.Modularity
{
    internal sealed class MicSwitchConfig : IPoeEyeConfigVersioned
    {
        public static readonly string DiscordInviteLink = @"https://discord.gg/BExRm22";
        
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

        [Obsolete("Replaced with OverlayConfig")]
        public Point? OverlayLocation { get; set; }

        [Obsolete("Replaced with OverlayConfig")]
        public Size? OverlaySize { get; set; }

        [Obsolete("Replaced with OverlayConfig")]
        public Rectangle? OverlayBounds { get; set; }

        [Obsolete("Replaced with OverlayConfig")]
        public float? OverlayOpacity { get; set; }

        [Obsolete("Replaced with OverlayVisibilityMode")]
        public bool? OverlayEnabled { get; set; }

        [Obsolete("Replaced with OverlayVisibilityMode")]
        public byte[] MutedMicrophoneIcon { get; set; }
        
        [Obsolete("Replaced with OverlayVisibilityMode")]
        public byte[] MicrophoneIcon { get; set; }

        public int Version { get; set; } = 1;
    }
}