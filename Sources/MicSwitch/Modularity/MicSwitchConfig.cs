using System;
using System.Drawing;
using System.Windows;
using MicSwitch.MainWindow.Models;
using MicSwitch.Services;
using Newtonsoft.Json;
using PoeShared.Audio.Models;
using PoeShared.Modularity;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace MicSwitch.Modularity
{
    internal sealed class MicSwitchConfig : IPoeEyeConfigVersioned
    {
        public static readonly string DiscordInviteLink = @"https://discord.gg/BExRm22";
        
        public bool StartMinimized { get; set; }

        public MicrophoneLineData MicrophoneLineId { get; set; }
        
        public string OutputDeviceId { get; set; }
        
        public bool MinimizeOnClose { get; set; } = true;
        
        public bool VolumeControlEnabled { get; set; } = false;
        
        public Rect? MainWindowBounds { get; set; }

        [Obsolete("Replaced with Notifications")]
        public TwoStateNotification? Notification { get; set; }
        
        public TwoStateNotification Notifications { get; set; } = new TwoStateNotification
        {
            On = "Beep300",
            Off = "Beep750"
        };

        public float NotificationVolume { get; set; } = 1;
        
        [Obsolete("Replaced with MicSwitchHotkeyConfig")]
        public MuteMode? MuteMode { get; set; }
        
        [Obsolete("Replaced with MicSwitchHotkeyConfig")]
        public bool? SuppressHotkey { get; set; }
        
        [Obsolete("Replaced with MicSwitchHotkeyConfig")]
        public string MicrophoneHotkey { get; set; }

        [Obsolete("Replaced with MicSwitchHotkeyConfig")]
        public string MicrophoneHotkeyAlt { get; set; }

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