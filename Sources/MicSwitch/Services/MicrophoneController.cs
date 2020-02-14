using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JetBrains.Annotations;
using log4net;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using PoeShared;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using ReactiveUI;
using Unity;

namespace MicSwitch.Services
{
    internal sealed class MicrophoneController : DisposableReactiveObject, IMicrophoneController
    {
        private readonly IMicrophoneProvider microphoneProvider;
        private static readonly ILog Log = LogManager.GetLogger(typeof(MicrophoneController));
        private static readonly TimeSpan SamplingInterval = TimeSpan.FromMilliseconds(50);

        private MicrophoneLineData lineId;

        private MMDevice mixerControl;

        public MicrophoneController(
            IMicrophoneProvider microphoneProvider)
        {
            this.microphoneProvider = microphoneProvider;
            this.WhenAnyValue(x => x.LineId)
                .Subscribe(InitializeLine)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.MixerControl)
                .Do(mixer =>
                {
                    if (mixer == null)
                    {
                        Log.Info($"Unbound controller from line #{lineId}");
                    }
                    else
                    {
                        var description = new
                        {
                            mixer.ID,
                            mixer.State,
                            mixer.FriendlyName,
                            mixer.DeviceFriendlyName,
                            mixer.IconPath,
                            AudioClientFormat = mixer.AudioClient?.MixFormat
                        };
                        Log.Info($"Successfully bound to line #{lineId}, volume: {VolumePercent}, isOn: {Mute}, line: {description}");
                    }
                })
                .Select(mixer => mixer != null
                    ? Observable.FromEvent<AudioEndpointVolumeNotificationDelegate, AudioVolumeNotificationData>(
                        h => mixer.AudioEndpointVolume.OnVolumeNotification += h,
                        h => mixer.AudioEndpointVolume.OnVolumeNotification -= h)
                      .StartWith(new AudioVolumeNotificationData(mixer.AudioEndpointVolume.NotificationGuid, mixer.AudioEndpointVolume.Mute, mixer.AudioEndpointVolume.MasterVolumeLevel, new float[0], Guid.Empty))
                    : Observable.Never<AudioVolumeNotificationData>().StartWithDefault())
                .Switch()
                .Sample(SamplingInterval)
                .Where(x => x != null)
                .Do(evt =>
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.Debug($"[#{LineId}] Volume notification: {evt.DumpToTextRaw()}");
                    }
                })
                .Subscribe(Update, Log.HandleException)
                .AddTo(Anchors);
        }

        public MMDevice MixerControl
        {
            get => mixerControl;
            private set => this.RaiseAndSetIfChanged(ref mixerControl, value);
        }

        public double? VolumePercent
        {
            get => mixerControl?.AudioEndpointVolume?.MasterVolumeLevelScalar;
            set
            {
                if (value == null || mixerControl?.AudioEndpointVolume == null)
                {
                    return;
                }

                Log.Debug($"[#{LineId}] Setting volume to {value.Value} (current: {VolumePercent})");

                mixerControl.AudioEndpointVolume.MasterVolumeLevelScalar = (float) value.Value;
            }
        }

        public MicrophoneLineData LineId
        {
            get => lineId;
            set => this.RaiseAndSetIfChanged(ref lineId, value);
        }

        public bool? Mute
        {
            get => mixerControl?.AudioEndpointVolume?.Mute;
            set
            {
                if (value == null || mixerControl?.AudioEndpointVolume == null)
                {
                    return;
                }

                if (value.Value)
                {
                    Log.Debug($"[#{LineId}] Disabling mic");

                    mixerControl.AudioEndpointVolume.Mute = true;
                }
                else
                {
                    Log.Debug($"[#{LineId}] Enabling mic");
                    mixerControl.AudioEndpointVolume.Mute = false;
                }
            }
        }

        private static IObservable<WaveInAudioExEventArgs> ToAudioStream(MMDevice mixer)
        {
            Log.Debug($"[AudioStream #{mixer.ID}] Initializing recording...");

            var waveIn = new WasapiCapture(mixer);
            var waveFormat = waveIn.WaveFormat;
            Disposable.Create(() =>
            {
                Log.Debug($"[AudioStream #{mixer.ID}] Stopping recording...");
                waveIn.StopRecording();
            });
            var result = Observable.FromEventPattern<WaveInEventArgs>(
                    h => waveIn.DataAvailable += h, h => waveIn.DataAvailable -= h)
                .Select(x => x.EventArgs)
                .Select(x => new WaveInAudioExEventArgs(x.Buffer, x.BytesRecorded, waveFormat));
            Log.Debug($"[AudioStream #{mixer.ID}] Starting recording...");
            waveIn.StartRecording();
            Log.Debug($"[AudioStream #{mixer.ID}] Streaming data...");

            return result;
        }

        private void Update()
        {
            this.RaisePropertyChanged(nameof(VolumePercent));
            this.RaisePropertyChanged(nameof(Mute));
        }

        private void InitializeLine()
        {
            Log.Info($"Binding to line ({lineId})...");
            VolumePercent = null;
            Mute = null;
            MixerControl = lineId.IsEmpty ? null : microphoneProvider.GetMixerControl(lineId.LineId);
        }
    }
}