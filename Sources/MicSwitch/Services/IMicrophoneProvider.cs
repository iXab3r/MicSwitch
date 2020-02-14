using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;
using NAudio.CoreAudioApi;

namespace MicSwitch.Services
{
    internal interface IMicrophoneProvider
    {
        [CanBeNull]
        MMDevice GetMixerControl([NotNull] string lineId);

        [NotNull]
        ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }
    }
}