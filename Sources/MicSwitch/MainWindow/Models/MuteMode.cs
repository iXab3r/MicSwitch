using System.ComponentModel;

namespace MicSwitch.MainWindow.Models
{
    internal enum MuteMode
    {
        [Description("Toggle Mute state on Hotkey press")]
        ToggleMute,
        [Description("Unmute microphone while Hotkey is held")]
        PushToTalk,
        [Description("Mute microphone while Hotkey is held")]
        PushToMute
    }
}