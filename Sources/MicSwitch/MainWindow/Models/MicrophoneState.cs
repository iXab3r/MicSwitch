using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MicSwitch.MainWindow.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum MicrophoneState
    {
        Any,
        Mute,
        Unmute
    }
}