using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class ComplexMMDeviceController : DisposableReactiveObject, IMMDeviceControllerEx
    {
        private static readonly Binder<ComplexMMDeviceController> Binder = new();
        private static readonly IFluentLog Log = typeof(ComplexMMDeviceController).PrepareLogger();

        static ComplexMMDeviceController()
        {
            Binder.Bind(x => x.SynchronizationIsEnabled).To(x => x.ActiveController.SynchronizationIsEnabled);
        }

        public ComplexMMDeviceController(
            IFactory<MultimediaDeviceController, IMMDeviceProvider> multimediaControllerFactory,
            IFactory<CollectionMMDevicesController, IReadOnlyObservableCollection<IMMDeviceController>> collectionControllerFactory,
            IMMDeviceProvider deviceProvider)
        {
            this.WhenAnyValue(x => x.DeviceId)
                .Select(deviceId => Observable.Using(() =>
                {
                    if (deviceId.LineId == MMDeviceId.All.LineId)
                    {
                        deviceProvider
                            .Devices
                            .ToObservableChangeSet()
                            .Filter(x => x.LineId != MMDeviceId.All.LineId)
                            .Transform(deviceId =>
                            {
                                var multimediaLine = multimediaControllerFactory.Create(deviceProvider);
                                multimediaLine.DeviceId = deviceId;
                                return (IMMDeviceController) multimediaLine;
                            })
                            .DisposeMany()
                            .BindToCollection(out var sources)
                            .AddKey(x => x.DeviceId)
                            .AsObservableCache();
                        return (IMMDeviceController)collectionControllerFactory.Create(sources);
                    }
                    else
                    {
                        var multimediaLine = multimediaControllerFactory.Create(deviceProvider);
                        multimediaLine.DeviceId = deviceId;
                        return multimediaLine;
                    }
                }, x => Observable.Return(x).Concat(Observable.Never<CollectionMMDevicesController>())))
                .Switch()
                .WithPrevious()
                .SubscribeSafe(x =>
                {
                    Log.Debug(() => $"Active controller is updated: {x}");
                    ActiveController = x.Current;
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.ActiveController.Volume)
                .SubscribeSafe(() => RaisePropertyChanged(nameof(Volume)), Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.ActiveController.Mute)
                .SubscribeSafe(() => RaisePropertyChanged(nameof(Mute)), Log.HandleUiException)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.ActiveController.IsConnected)
                .SubscribeSafe(() => RaisePropertyChanged(nameof(IsConnected)), Log.HandleUiException)
                .AddTo(Anchors);

            DeviceId = MMDeviceId.All;
            
            Binder.Attach(this).AddTo(Anchors);
        }

        public MMDeviceId DeviceId { get; set; }

        public bool? Mute
        {
            get => SafeRead(ActiveController, x => x.Mute);
            set => SafeAction(ActiveController, x => x.Mute = value);
        }

        public float? Volume
        {
            get => SafeRead(ActiveController, x => x.Volume);
            set => SafeAction(ActiveController, x => x.Volume = value);
        }

        public bool IsConnected => SafeRead(ActiveController, x => x.IsConnected);
        
        public bool SynchronizationIsEnabled { get; set; }

        public IMMDeviceController ActiveController { get; private set; }

        private static T SafeRead<T>(IMMDeviceController controller, Func<IMMDeviceController, T> func)
        {
            try
            {
                if (controller == null)
                {
                    throw new InvalidOperationException("Controller is not assigned");
                }

                return func(controller);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to apply operation to line {controller?.DeviceId}", e);
                return default;
            }
        }

        private static void SafeAction(IMMDeviceController controller, Action<IMMDeviceController> action)
        {
            try
            {
                if (controller == null)
                {
                    throw new InvalidOperationException("Controller is not assigned");
                }

                action(controller);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to apply operation to line {controller?.DeviceId}", e);
            }
        }
    }
}