using System;
using System.Linq;
using System.Reactive.Linq;
using Common.Logging;
using NAudio.CoreAudioApi;
using NAudio.Mixer;
using PoeShared;
using PoeShared.Scaffolding;
using ReactiveUI;

namespace MicSwitch
{
    internal sealed class MicrophoneController : DisposableReactiveObject, IMicrophoneController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MicrophoneController));
        
        private MicrophoneLineData lineId;

        private MMDevice mixerControl;

        public MicrophoneController()
        {
            this.WhenAnyValue(x => x.LineId)
                .Subscribe(InitializeLine)
                .AddTo(Anchors);
            
            Observable
                .Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100))
                .Subscribe(Update)
                .AddTo(Anchors);
        }

        public double? VolumePercent
        {
            get { return mixerControl?.AudioEndpointVolume.MasterVolumeLevelScalar; }
            set
            {
                if (mixerControl == null || value == null)
                {
                    return;
                }

                mixerControl.AudioEndpointVolume.MasterVolumeLevelScalar = (float)value.Value;
            }
        }

        public MicrophoneLineData LineId
        {
            get => lineId;
            set => this.RaiseAndSetIfChanged(ref lineId, value);
        }

        public bool? Mute
        {
            get => mixerControl?.AudioEndpointVolume.Mute;
            set
            {
                var currentVolume = VolumePercent;
                if (mixerControl == null || value == null || currentVolume == null)
                {
                    return;
                }

                if (value.Value)
                {
                    Log.Debug($"[#{LineId}] Disabling mic, setting volume to 0 current {VolumePercent}");

                    mixerControl.AudioEndpointVolume.Mute = true;
                }
                else
                {
                    Log.Debug($"[#{LineId}] Enabling mic, current {currentVolume}");
                    mixerControl.AudioEndpointVolume.Mute = false;
                }
            }
        }

        private void Update()
        {
            this.RaisePropertyChanged(nameof(VolumePercent));
            this.RaisePropertyChanged(nameof(Mute));
        }

        private void InitializeLine()
        {
            Log.Info($"Binding to line #{lineId}...");
            VolumePercent = null;
            Mute = null;
            mixerControl = lineId == null ? null : new MicrophoneProvider().GetMixerControl(lineId.LineId);
            if (mixerControl != null)
            {
                Log.Info($"Successfully bound to line #{lineId}, volume: {VolumePercent}, isOn: {Mute}");
            }
        }
    }
}