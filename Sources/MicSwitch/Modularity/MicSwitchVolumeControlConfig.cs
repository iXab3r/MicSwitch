using MicSwitch.MainWindow.Models;
using PoeShared.Audio.Models;

namespace MicSwitch.Modularity;

internal sealed class MicSwitchVolumeControlConfig : IPoeEyeConfigVersioned
{
    public bool IsEnabled { get; set; }
    public MMDeviceId DeviceId { get; set; }

    public HotkeyConfig HotkeyForToggle { get; set; }
    public HotkeyConfig HotkeyForMute { get; set; }
    public HotkeyConfig HotkeyForUnmute { get; set; }
        
    public HotkeyConfig HotkeyForVolumeDown { get; set; }
        
    public HotkeyConfig HotkeyForVolumeUp { get; set; }
        
    public int Version { get; set; }
}