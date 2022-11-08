using log4net;
using MicSwitch.Modularity;
using NAudio.CoreAudioApi;
using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal sealed class MultimediaDeviceController : DisposableReactiveObject, IMMDeviceControllerEx
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MultimediaDeviceController));
        private static readonly TimeSpan SamplingInterval = TimeSpan.FromMilliseconds(50);

        private readonly IMMDeviceProvider deviceProvider;

        public MultimediaDeviceController(IMMDeviceProvider deviceProvider)
        {
            this.deviceProvider = deviceProvider;
            this.WhenAnyValue(x => x.LineId)
                .Where(x => !x.IsEmpty)
                .SubscribeSafe(InitializeLine, Log.HandleException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.MixerControl)
                .DisposePrevious()
                .Do(mixer =>
                {
                    if (mixer == null)
                    {
                        if (!LineId.IsEmpty)
                        {
                            Log.Info($"Unbound controller from line #{LineId}");
                        }
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
                        Log.Info($"Successfully bound to line #{LineId}, volume: {VolumePercent}, isOn: {Mute}, line: {description}");
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
                        Log.Debug($"[#{LineId}] Volume notification: {evt.Dump()}");
                    }
                })
                .SubscribeSafe(Update, Log.HandleException)
                .AddTo(Anchors);
        }

        public MMDevice MixerControl { get; private set; }

        public double? VolumePercent
        {
            get => MixerControl?.AudioEndpointVolume?.MasterVolumeLevelScalar;
            set
            {
                if (value == null || MixerControl?.AudioEndpointVolume == null)
                {
                    return;
                }
                
                Log.Debug($"[#{LineId}] Setting volume to {value.Value} (current: {VolumePercent})");
                MixerControl.AudioEndpointVolume.MasterVolumeLevelScalar = (float) value.Value;
            }
        }

        public MMDeviceId LineId { get; set; }
        
        public bool? Mute
        {
            get => MixerControl?.AudioEndpointVolume?.Mute;
            set
            {
                if (value == null || MixerControl?.AudioEndpointVolume == null)
                {
                    return;
                }

                if (value.Value)
                {
                    Log.Debug($"[#{LineId}] Disabling mic");
                    MixerControl.AudioEndpointVolume.Mute = true;
                }
                else
                {
                    Log.Debug($"[#{LineId}] Enabling mic");
                    MixerControl.AudioEndpointVolume.Mute = false;
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
            Log.Info($"Binding to line ({LineId})...");
            VolumePercent = null;
            Mute = null;
            MixerControl = LineId.IsEmpty ? null : deviceProvider.GetMixerControl(LineId.LineId);
        }
    }
}