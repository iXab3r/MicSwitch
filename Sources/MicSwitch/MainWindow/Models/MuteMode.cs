using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MicSwitch.MainWindow.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
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