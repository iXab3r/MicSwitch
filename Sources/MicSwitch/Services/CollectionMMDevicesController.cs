using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class CollectionMMDevicesController : DisposableReactiveObjectWithLogger, IMMDeviceController
    {
        private static readonly Binder<CollectionMMDevicesController> Binder = new();

        private readonly IReadOnlyObservableCollection<IMMDeviceController> devices;

        static CollectionMMDevicesController()
        {
            Binder.Bind(x => x.devices.Any(y => y.IsConnected)).To(x => x.IsConnected);
            Binder.BindAction(x => x.devices.ForEach(device => SetSynchronizationState(device, x.SynchronizationIsEnabled)));
        }

        public CollectionMMDevicesController(IReadOnlyObservableCollection<IMMDeviceController> devices)
        {
            DeviceId = MMDeviceId.All;
            this.devices = devices;

            devices.ToObservableChangeSet()
                .OnItemAdded(newDevice =>
                {
                    Log.Debug(() => $"New device {newDevice.DeviceId} detected in All devices mode, assigning following parameters: {new {Mute, VolumePercent = Volume}}");
                    newDevice.SynchronizationIsEnabled = SynchronizationIsEnabled;
                    if (Mute != null)
                    {
                        SetMuteSafe(Log, newDevice, Mute);
                    }

                    if (Volume != null)
                    {
                        SetVolumeSafe(Log, newDevice, Volume);
                    }
                }).SubscribeToErrors(Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.Volume)
                .Skip(1)
                .EnableIf(this.WhenAnyValue(y => y.SynchronizationIsEnabled))
                .Subscribe(volume =>
                {
                    Log.Debug(() => $"Setting {nameof(Volume)} to {volume} for {devices.Count} devices: {devices.DumpToString()}");
                    devices.ForEach(device => SetVolumeSafe(Log, device, volume));
                }, Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.Mute)
                .Skip(1)
                .EnableIf(this.WhenAnyValue(y => y.SynchronizationIsEnabled))
                .SubscribeSafe(mute =>
                {
                    Log.Debug(() => $"Setting {nameof(Mute)} to {mute} for {devices.Count}");
                    devices.ForEach(device => SetMuteSafe(Log, device, mute));
                }, Log.HandleUiException)
                .AddTo(Anchors);

            devices.ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Mute, notifyOnInitialValue: false)
                .Do(x => Log.Debug(() => $"Device {x.Sender} {nameof(x.Sender.Mute)} has changed to {x.Value}"))
                .Subscribe(x => { Mute = x.Value; }, Log.HandleUiException)
                .AddTo(Anchors);

            devices.ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Volume, notifyOnInitialValue: false)
                .Do(x => Log.Debug(() => $"Device {x.Sender} {nameof(x.Sender.Volume)} has changed to {x.Value}"))
                .Subscribe(x => { Volume = x.Value; }, Log.HandleUiException)
                .AddTo(Anchors);

            Binder.Attach(this).AddTo(Anchors);

            Disposable.Create(() => Log.Debug(() => $"Controller of multiple devices was disposed, devices: {devices.DumpToString()}")).AddTo(Anchors);
        }

        public MMDeviceId DeviceId { get; set; }

        public bool? Mute { get; set; }

        public float? Volume { get; set; }

        public bool IsConnected { get; [UsedImplicitly] private set; }

        public bool SynchronizationIsEnabled { get; set; }

        private static void SetSynchronizationState(IMMDeviceController deviceController, bool value)
        {
            deviceController.SynchronizationIsEnabled = value;
        }
        
        private static void SetVolumeSafe(IFluentLog log, IMMDeviceController deviceController, float? value)
        {
            try
            {
                log.Debug(() => $"Setting Volume to {value}, device: {deviceController}");
                deviceController.Volume = value;
            }
            catch (Exception e)
            {
                log.Warn($"Failed to set Volume to {value}, device: {deviceController}");
            }
        }
        
        private static void SetMuteSafe(IFluentLog log, IMMDeviceController deviceController, bool? value)
        {
            try
            {
                log.Debug(() => $"Setting Mute to {value}, device: {deviceController}");
                deviceController.Mute = value;
            }
            catch (Exception e)
            {
                log.Warn($"Failed to set Mute to {value}, device: {deviceController}");
            }
        }
    }
}