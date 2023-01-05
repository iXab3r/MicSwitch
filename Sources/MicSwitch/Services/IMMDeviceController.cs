using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMMDeviceController : IDisposableReactiveObject
    {
        MMDeviceId DeviceId { get; set; }

        bool? Mute { get; set; }

        float? Volume { get; set; }
        
        bool IsConnected { get; }
        
        /// <summary>
        /// If true, controller will apply two-way sync of Volume, Mute and other state
        /// </summary>
        bool SynchronizationIsEnabled { get; set; }
    }
}