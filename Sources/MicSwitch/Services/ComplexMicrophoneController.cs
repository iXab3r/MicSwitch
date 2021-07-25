using System;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Reactive.Linq;
using log4net;
using PoeShared;
using PoeShared.Audio.Models;
using PoeShared.Prism;
using PoeShared.Scaffolding;

namespace MicSwitch.Services
{
    internal sealed class ComplexMicrophoneController : DisposableReactiveObject, IMicrophoneControllerEx
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ComplexMicrophoneController));

        private readonly IObservableCache<IMicrophoneController, MicrophoneLineData> microphones;
        private IMicrophoneController activeController;
        private MicrophoneLineData lineId;

        public ComplexMicrophoneController(
            IFactory<MultimediaMicrophoneController, IMicrophoneProvider> multimediaControllerFactory,
            IMicrophoneProvider microphoneProvider)
        {
            microphones = microphoneProvider
                .Microphones
                .ToObservableChangeSet()
                .Filter(x => x.LineId != MicrophoneLineData.All.LineId)
                .Transform(x =>
                {
                    var multimediaLine = multimediaControllerFactory.Create(microphoneProvider);
                    multimediaLine.LineId = x;
                    return (IMicrophoneController) multimediaLine;
                })
                .Bind(out var sources)
                .AddKey(x => x.LineId)
                .AsObservableCache();

            var allLinesController = new AllMicrophonesController(sources).AddTo(Anchors);

            this.WhenAnyValue(x => x.LineId)
                .Select(x => microphones.Lookup(x))
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
                .Select(x => x?.LineId.LineId == MicrophoneLineData.All.LineId
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

            LineId = MicrophoneLineData.All;
        }

        public MicrophoneLineData LineId
        {
            get => lineId;
            set => RaiseAndSetIfChanged(ref lineId, value);
        }

        public bool? Mute
        {
            get => SafeRead(activeController, x => x.Mute);
            set => SafeAction(activeController, x => x.Mute = value);
        }

        public double? VolumePercent
        {
            get => SafeRead(activeController, x => x.VolumePercent);
            set => SafeAction(activeController, x => x.VolumePercent = value);
        }

        public IMicrophoneController ActiveController
        {
            get => activeController;
            private set => RaiseAndSetIfChanged(ref activeController, value);
        }

        private static T SafeRead<T>(IMicrophoneController controller, Func<IMicrophoneController, T> func)
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
                Log.Error($"Failed to apply operation to line {controller?.LineId}");
                return default;
            }
        }

        private static void SafeAction(IMicrophoneController controller, Action<IMicrophoneController> action)
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
                Log.Error($"Failed to apply operation to line {controller?.LineId}");
            }
        }
    }
}