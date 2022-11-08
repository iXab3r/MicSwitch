using log4net;
using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class AllMMDevicesController : DisposableReactiveObject, IMMDeviceController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AllMMDevicesController));

        private readonly ReadOnlyObservableCollection<IMMDeviceController> devices;

        public AllMMDevicesController(ReadOnlyObservableCollection<IMMDeviceController> devices)
        {
            this.devices = devices;
            LineId = MMDeviceId.All;

            devices
                .ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Mute)
                .StartWithDefault()
                .SubscribeSafe(() => RaisePropertyChanged(nameof(Mute)), Log.HandleUiException)
                .AddTo(Anchors);
            
            devices
                .ToObservableChangeSet()
                .WhenPropertyChanged(x => x.VolumePercent)
                .StartWithDefault()
                .SubscribeSafe(() => RaisePropertyChanged(nameof(VolumePercent)), Log.HandleUiException)
                .AddTo(Anchors);
        }

        public MMDeviceId LineId { get; }

        public bool? Mute
        {
            get => devices.Any() ? devices.All(x => x.Mute == true) : default;
            set => devices.ForEach(x => x.Mute = value);
        }

        public double? VolumePercent
        {
            get => devices.Any() ? devices.Min(x => x.VolumePercent) : default;
            set => devices.ForEach(x => x.VolumePercent = value);
        }
    }
}