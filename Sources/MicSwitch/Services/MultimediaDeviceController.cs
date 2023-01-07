using MicSwitch.Modularity;
using NAudio.CoreAudioApi;
using PoeShared.Audio.Models;
using PropertyChanged;

namespace MicSwitch.Services
{
    internal sealed class MultimediaDeviceController : DisposableReactiveObject, IMMDeviceController
    {
        private static readonly Binder<MultimediaDeviceController> Binder = new();

        private static readonly IFluentLog Log = typeof(MultimediaDeviceController).PrepareLogger();
        private static readonly TimeSpan SamplingInterval = TimeSpan.FromMilliseconds(50);

        private readonly IMMDeviceProvider deviceProvider;

        static MultimediaDeviceController()
        {
            Binder.Bind(x => x.MixerControl != null).To(x => x.IsConnected);
        }

        public MultimediaDeviceController(IMMDeviceProvider deviceProvider)
        {
            this.deviceProvider = deviceProvider;

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
                        Log.Info($"Successfully bound to line #{DeviceId}, volume: {Volume}, isOn: {Mute}");
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

            this.WhenAnyValue(x => x.DeviceId)
                .Select(x => x.IsEmpty ? Observable.Return(default(MMDevice)) : deviceProvider.DevicesById.WatchCurrentValue(x))
                .Switch()
                .Subscribe(x =>
                {
                    Volume = null;
                    Mute = null;
                    MixerControl = x;
                })
                .AddTo(Anchors);
            
            Binder.Attach(this).AddTo(Anchors);
            Disposable.Create(() => Log.Debug(() => $"Controller of multimedia device {DeviceId} was disposed")).AddTo(Anchors);
        }

        public MMDevice MixerControl { get; private set; }

        public bool IsConnected { get; [UsedImplicitly] private set; }

        public bool SynchronizationIsEnabled { get; set; }

        [DoNotNotify]
        public float? Volume
        {
            get => MixerControl?.AudioEndpointVolume?.MasterVolumeLevelScalar;
            set
            {
                if (value == null || value.Equals(Volume))
                {
                    return;
                }
                if (!SynchronizationIsEnabled)
                {
                    throw new InvalidOperationException($"Synchronization is disabled for device {DeviceId}");
                }
                
                var endpoint = MixerControl?.AudioEndpointVolume;
                if (endpoint == null)
                {
                    return;
                }

                Log.WithSuffix(DeviceId).Debug($"Setting volume to {value.Value} (current: {Volume})");
                endpoint.MasterVolumeLevelScalar = value.Value;
            }
        }

        public MMDeviceId DeviceId { get; set; }
        
        [DoNotNotify]
        public bool? Mute
        {
            get => MixerControl?.AudioEndpointVolume?.Mute;
            set
            {
                if (value == null || value.Equals(Mute))
                {
                    return;
                }
                
                var endpoint = MixerControl?.AudioEndpointVolume;
                if (endpoint == null)
                {
                    return;
                }

                if (value.Value)
                {
                    Log.WithSuffix(DeviceId).Debug($"Disabling mic");
                    endpoint.Mute = true;
                }
                else
                {
                    Log.WithSuffix(DeviceId).Debug($"Enabling mic");
                    endpoint.Mute = false;
                }
            }
        }

        private void Update()
        {
            this.RaisePropertyChanged(nameof(Volume));
            this.RaisePropertyChanged(nameof(Mute));
        }

        protected override void FormatToString(ToStringBuilder builder)
        {
            base.FormatToString(builder);
            builder.AppendParameter(nameof(DeviceId), DeviceId);
            builder.AppendParameter(nameof(Volume), Volume);
            builder.AppendParameter(nameof(Mute), Mute);
        }
    }
}