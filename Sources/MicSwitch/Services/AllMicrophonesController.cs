using log4net;
using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class AllMicrophonesController : DisposableReactiveObject, IMicrophoneController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AllMicrophonesController));

        private readonly ReadOnlyObservableCollection<IMicrophoneController> microphones;
        private MMDeviceLineData lineId;

        public AllMicrophonesController(ReadOnlyObservableCollection<IMicrophoneController> microphones)
        {
            this.microphones = microphones;
            LineId = MMDeviceLineData.All;

            microphones
                .ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Mute)
                .StartWithDefault()
                .SubscribeSafe(() => RaisePropertyChanged(nameof(Mute)), Log.HandleUiException)
                .AddTo(Anchors);
            
            microphones
                .ToObservableChangeSet()
                .WhenPropertyChanged(x => x.VolumePercent)
                .StartWithDefault()
                .SubscribeSafe(() => RaisePropertyChanged(nameof(VolumePercent)), Log.HandleUiException)
                .AddTo(Anchors);
        }

        public MMDeviceLineData LineId
        {
            get => lineId;
            private set => RaiseAndSetIfChanged(ref lineId, value);
        }

        public bool? Mute
        {
            get => microphones.Any() ? microphones.All(x => x.Mute == true) : default;
            set => microphones.ForEach(x => x.Mute = value);
        }

        public double? VolumePercent
        {
            get => microphones.Any() ? microphones.Min(x => x.VolumePercent) : default;
            set => microphones.ForEach(x => x.VolumePercent = value);
        }
    }
}