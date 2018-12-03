using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Common.Logging;
using NAudio.CoreAudioApi;
using NAudio.Mixer;
using NAudio.Wave;
using PoeShared;
using PoeShared.Scaffolding;
using ReactiveUI;

namespace MicSwitch
{
    internal sealed class MicrophoneController : DisposableReactiveObject, IMicrophoneController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MicrophoneController));
        private static readonly TimeSpan SamplingInterval = TimeSpan.FromMilliseconds(100);
        
        private MicrophoneLineData lineId;

        private MMDevice mixerControl;

        public MicrophoneController()
        {
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
                            AudioClientFormat = mixer.AudioClient?.MixFormat,
                        };
                        Log.Info($"Successfully bound to line #{lineId}, volume: {VolumePercent}, isOn: {Mute}, line: {description}");
                    }
                })
                .Select(mixer => mixer != null ? 
                    Observable.FromEvent<AudioEndpointVolumeNotificationDelegate, AudioVolumeNotificationData>(
                        h => mixer.AudioEndpointVolume.OnVolumeNotification += h,
                        h => mixer.AudioEndpointVolume.OnVolumeNotification -= h) 
                    : Observable.Never<AudioVolumeNotificationData>())
                .Switch()
                .Sample(SamplingInterval)
                .Where(x => x != null)
                .Do(evt =>
                {
                    if (Log.IsTraceEnabled)
                    {
                        Log.Trace($"[#{LineId}] Volume notification: {evt.DumpToTextRaw()}");
                    }
                })
                .Subscribe(Update, Log.HandleException)
                .AddTo(Anchors);
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

        public IObservable<WaveInAudioExEventArgs> AudioStream
        {
            get
            {
                var mixer = mixerControl;
                if (mixer == null || mixer.AudioClient == null)
                {
                    return Observable.Empty<WaveInAudioExEventArgs>();
                }
                Log.Debug($"[AudioStream #{mixer.ID}] Preparing recording source...");
                return Observable.Create<WaveInAudioExEventArgs>(observer =>
                {
                    Log.Debug($"[AudioStream #{mixer.ID}] Initializing recording...");

                    var waveIn = new WasapiCapture(mixer);
                    var waveFormat = waveIn.WaveFormat;
                    var anchors = new CompositeDisposable();
                    Disposable.Create(() =>
                    {
                        Log.Debug($"[AudioStream #{mixer.ID}] Stopping recording...");
                        waveIn.StopRecording();
                    });
                    Observable.FromEventPattern<WaveInEventArgs>(
                            h => waveIn.DataAvailable += h, h => waveIn.DataAvailable -= h)
                        .Select(x => x.EventArgs)
                        .Select(x => new WaveInAudioExEventArgs(x.Buffer, x.BytesRecorded, waveFormat))
                        .Subscribe(observer)
                        .AddTo(anchors);
                    Log.Debug($"[AudioStream #{mixer.ID}] Starting recording...");
                    waveIn.StartRecording();
                    Log.Debug($"[AudioStream #{mixer.ID}] Streaming data...");

                    return anchors;
                });

            }
        }
        
        public MMDevice MixerControl
        {
            get => mixerControl;
            set => this.RaiseAndSetIfChanged(ref mixerControl, value);
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
            MixerControl = lineId.IsEmpty ? null : new MicrophoneProvider().GetMixerControl(lineId.LineId);
        }
    }
}