using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMMDeviceController : IDisposableReactiveObject
    {
        MMDeviceId DeviceId { get; set; }

        bool? Mute { get; set; }

        float? Volume { get; set; }
    }
}