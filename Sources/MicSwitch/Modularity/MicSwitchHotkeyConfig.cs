using MicSwitch.MainWindow.Models;

namespace MicSwitch.Modularity
{
    internal sealed class MicSwitchHotkeyConfig : IPoeEyeConfigVersioned
    {
        public HotkeyConfig Hotkey { get; set; }

        public MuteMode MuteMode { get; set; } = MuteMode.ToggleMute;
        
        public HotkeyConfig HotkeyForMute { get; set; }
        
        public HotkeyConfig HotkeyForUnmute { get; set; }
        
        public HotkeyConfig HotkeyForToggle { get; set; }
        
        public HotkeyConfig HotkeyForPushToTalk { get; set; }
        
        public HotkeyConfig HotkeyForPushToMute { get; set; }
        
        public bool EnableAdvancedHotkeys { get; set; }
        

        public MicrophoneState InitialMicrophoneState { get; set; } = MicrophoneState.Any;
        
        public int Version { get; set; } = 1;
    }
}