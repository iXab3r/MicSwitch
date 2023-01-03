using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class CollectionMMDevicesController : DisposableReactiveObjectWithLogger, IMMDeviceController
    {
        private static readonly Binder<CollectionMMDevicesController> Binder = new();

        private readonly IReadOnlyObservableCollection<IMMDeviceController> devices;
        private readonly SharedResourceLatch updateLatch = new("Multiple devices update latch");

        static CollectionMMDevicesController()
        {
            Binder.Bind(x => x.devices.Any(y => y.IsConnected)).To(x => x.IsConnected);
        }

        public CollectionMMDevicesController(IReadOnlyObservableCollection<IMMDeviceController> devices)
        {
            DeviceId = MMDeviceId.All;
            this.devices = devices;

            devices.ToObservableChangeSet()
                .OnItemAdded(newDevice =>
                {
                    Log.Debug(() => $"New device {newDevice.DeviceId} detected in All devices mode, assigning following parameters: {new {Mute, VolumePercent = Volume}}");
                    if (Mute != null)
                    {
                        newDevice.Mute = Mute;
                    }

                    if (Volume != null)
                    {
                        newDevice.Volume = Volume;
                    }
                }).SubscribeToErrors(Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.Volume)
                .Skip(1)
                .Subscribe(x =>
                {
                    Log.Debug(() => $"Setting {nameof(Volume)} to {x} for {devices.Count} devices: {devices.DumpToString()}");
                    using (updateLatch.Rent())
                    {
                        devices.ForEach(y => y.Volume = x);
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.Mute)
                .Skip(1)
                .SubscribeSafe(x =>
                {
                    Log.Debug(() => $"Setting {nameof(Mute)} to {x} for {devices.Count}");
                    using (updateLatch.Rent())
                    {
                        devices.ForEach(y => y.Mute = x);
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            devices.ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Mute, notifyOnInitialValue: false)
                .Do(x => Log.Debug(() => $"Device {x.Sender} {nameof(x.Sender.Mute)} has changed to {x.Value}"))
                .Where(x => !updateLatch.IsBusy)
                .Subscribe(x =>
                {
                    Mute = x.Value;
                }, Log.HandleUiException)
                .AddTo(Anchors);

            devices.ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Volume, notifyOnInitialValue: false)
                .Do(x => Log.Debug(() => $"Device {x.Sender} {nameof(x.Sender.Volume)} has changed to {x.Value}"))
                .Where(x => !updateLatch.IsBusy)
                .Subscribe(x =>
                {
                    Volume = x.Value;
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            Binder.Attach(this).AddTo(Anchors);

            Disposable.Create(() => Log.Debug(() => $"Controller of multiple devices was disposed, devices: {devices.DumpToString()}")).AddTo(Anchors);
        }

        public MMDeviceId DeviceId { get; set; }

        public bool? Mute { get; set; }

        public float? Volume { get; set; }
        
        public bool IsConnected { get; [UsedImplicitly] private set; }
    }
}