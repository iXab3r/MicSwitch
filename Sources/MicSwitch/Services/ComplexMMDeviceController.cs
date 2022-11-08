using log4net;
using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class ComplexMMDeviceController : DisposableReactiveObject, IMMDeviceControllerEx
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ComplexMMDeviceController));

        private readonly IObservableCache<IMMDeviceController, MMDeviceId> devices;

        public ComplexMMDeviceController(
            IFactory<MultimediaDeviceController, IMMDeviceProvider> multimediaControllerFactory,
            IMMDeviceProvider mmDeviceProvider)
        {
            devices = mmDeviceProvider
                .Microphones
                .ToObservableChangeSet()
                .Filter(x => x.LineId != MMDeviceId.All.LineId)
                .Transform(x =>
                {
                    var multimediaLine = multimediaControllerFactory.Create(mmDeviceProvider);
                    multimediaLine.LineId = x;
                    return (IMMDeviceController) multimediaLine;
                })
                .Bind(out var sources)
                .AddKey(x => x.LineId)
                .AsObservableCache();

            var allLinesController = new AllMMDevicesController(sources).AddTo(Anchors);

            this.WhenAnyValue(x => x.LineId)
                .Select(x => devices.Lookup(x))
                .Select(x => x.HasValue ? x.Value : allLinesController)
                .SubscribeSafe(x => ActiveController = x, Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.ActiveController.VolumePercent)
                .SubscribeSafe(() => RaisePropertyChanged(nameof(VolumePercent)), Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.ActiveController.Mute)
                .SubscribeSafe(() => RaisePropertyChanged(nameof(Mute)), Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.ActiveController)
                .Select(x => x?.LineId.LineId == MMDeviceId.All.LineId
                    ? sources.ToObservableChangeSet().OnItemAdded(newMicrophone =>
                    {
                        Log.Debug($"New microphone {newMicrophone.LineId} detected in All microphones mode, assigning following parameters: {new {Mute, VolumePercent}}");
                        newMicrophone.Mute = Mute;
                        newMicrophone.VolumePercent = VolumePercent;
                    })
                    : Observable.Empty<IChangeSet>())
                .Switch()
                .SubscribeToErrors(Log.HandleException)
                .AddTo(Anchors);

            LineId = MMDeviceId.All;
        }

        public MMDeviceId LineId { get; set; }

        public bool? Mute
        {
            get => SafeRead(ActiveController, x => x.Mute);
            set => SafeAction(ActiveController, x => x.Mute = value);
        }

        public double? VolumePercent
        {
            get => SafeRead(ActiveController, x => x.VolumePercent);
            set => SafeAction(ActiveController, x => x.VolumePercent = value);
        }

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
                Log.Error($"Failed to apply operation to line {controller?.LineId}", e);
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
                Log.Error($"Failed to apply operation to line {controller?.LineId}", e);
            }
        }
    }
}