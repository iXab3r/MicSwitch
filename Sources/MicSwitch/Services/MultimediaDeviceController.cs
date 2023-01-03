using MicSwitch.Modularity;
using NAudio.CoreAudioApi;
using PoeShared.Audio.Models;
using PropertyChanged;

namespace MicSwitch.Services
{
    internal sealed class MultimediaDeviceController : DisposableReactiveObject, IMMDeviceController
    {
        private static readonly IFluentLog Log = typeof(MultimediaDeviceController).PrepareLogger();
        private static readonly TimeSpan SamplingInterval = TimeSpan.FromMilliseconds(50);

        private readonly IMMDeviceProvider deviceProvider;

        public MultimediaDeviceController(IMMDeviceProvider deviceProvider)
        {
            this.deviceProvider = deviceProvider;
            this.WhenAnyValue(x => x.DeviceId)
                .Where(x => !x.IsEmpty)
                .SubscribeSafe(InitializeLine, Log.HandleException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.MixerControl)
                .DisposePrevious()
                .Do(mixer =>
                {
                    if (mixer == null)
                    {
                        if (!DeviceId.IsEmpty)
                        {
                            Log.Info($"Unbound controller from line #{DeviceId}");
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
                        Log.Info($"Successfully bound to line #{DeviceId}, volume: {Volume}, isOn: {Mute}, line: {description}");
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
                        Log.WithSuffix(DeviceId).Debug($"Volume notification: {new { evt.Muted, evt.MasterVolume }}");
                    }
                })
                .SubscribeSafe(Update, Log.HandleException)
                .AddTo(Anchors);
        }

        public MMDevice MixerControl { get; private set; }

        [DoNotNotify]
        public float? Volume
        {
            get => MixerControl?.AudioEndpointVolume?.MasterVolumeLevelScalar;
            set
            {
                if (value == null || MixerControl?.AudioEndpointVolume == null)
                {
                    return;
                }
                
                Log.WithSuffix(DeviceId).Debug($"Setting volume to {value.Value} (current: {Volume})");
                MixerControl.AudioEndpointVolume.MasterVolumeLevelScalar = value.Value;
            }
        }

        public MMDeviceId DeviceId { get; set; }
        
        [DoNotNotify]
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
                    Log.WithSuffix(DeviceId).Debug($"Disabling mic");
                    MixerControl.AudioEndpointVolume.Mute = true;
                }
                else
                {
                    Log.WithSuffix(DeviceId).Debug($"Enabling mic");
                    MixerControl.AudioEndpointVolume.Mute = false;
                }
            }
        }

        private void Update()
        {
            this.RaisePropertyChanged(nameof(Volume));
            this.RaisePropertyChanged(nameof(Mute));
        }

        private void InitializeLine()
        {
            Log.Info($"Binding to line ({DeviceId})...");
            Volume = null;
            Mute = null;
            MixerControl = DeviceId.IsEmpty ? null : deviceProvider.GetMixerControl(DeviceId.LineId);
        }
    }
}