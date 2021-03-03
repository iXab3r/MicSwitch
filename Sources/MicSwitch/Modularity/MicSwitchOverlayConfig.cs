using System.Drawing;
using MicSwitch.MainWindow.Models;
using PoeShared.Modularity;
using PoeShared.Native;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace MicSwitch.Modularity
{
    internal sealed class MicSwitchOverlayConfig : IPoeEyeConfigVersioned, IOverlayConfig
    {
        public Point? OverlayLocation { get; set; }
        
        public Size? OverlaySize { get; set; }
        
        public Rectangle OverlayBounds { get; set; }

        public float OverlayOpacity { get; set; }
        
        public OverlayVisibilityMode OverlayVisibilityMode { get; set; } = OverlayVisibilityMode.Always;
        
        public byte[] MutedMicrophoneIcon { get; set; }
        
        public byte[] MicrophoneIcon { get; set; }

        public int Version { get; set; } = 1;
    }
}