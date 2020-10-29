using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData;
using PoeShared.Scaffolding;

namespace MicSwitch.Services
{
    internal sealed class AllMicrophonesController : DisposableReactiveObject, IMicrophoneController
    {
        private readonly ReadOnlyObservableCollection<IMicrophoneController> microphones;
        private MicrophoneLineData lineId;

        public AllMicrophonesController(ReadOnlyObservableCollection<IMicrophoneController> microphones)
        {
            this.microphones = microphones;
            LineId = MicrophoneLineData.All;

            microphones
                .ToObservableChangeSet()
                .WhenPropertyChanged(x => x.Mute)
                .StartWithDefault()
                .Subscribe(() => RaisePropertyChanged(nameof(Mute)))
                .AddTo(Anchors);
            
            microphones
                .ToObservableChangeSet()
                .WhenPropertyChanged(x => x.VolumePercent)
                .StartWithDefault()
                .Subscribe(() => RaisePropertyChanged(nameof(VolumePercent)))
                .AddTo(Anchors);
        }

        public MicrophoneLineData LineId
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