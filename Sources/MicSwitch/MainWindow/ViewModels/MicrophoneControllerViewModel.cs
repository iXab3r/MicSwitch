using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Services;
using NAudio.CoreAudioApi;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels;

internal sealed class MicrophoneControllerViewModel : MediaControllerBase<MicSwitchHotkeyConfig>, IMicrophoneControllerViewModel
{
    private static readonly Binder<MicrophoneControllerViewModel> Binder = new();

    static MicrophoneControllerViewModel()
    {
    }

    public MicrophoneControllerViewModel(
        IMMCaptureDeviceProvider deviceProvider,
        IFactory<IMMDeviceControllerEx, IMMDeviceProvider> deviceControllerFactory,
        IComplexHotkeyTracker hotkeyTracker,
        IFactory<IHotkeyTracker> hotkeyTrackerFactory,
        IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory,
        IConfigProvider<MicSwitchConfig> configProvider,
        IConfigProvider<MicSwitchHotkeyConfig> hotkeyConfigProvider,
        [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler) : base(deviceProvider, deviceControllerFactory.Create(deviceProvider), hotkeyTrackerFactory, hotkeyEditorFactory, hotkeyConfigProvider, uiScheduler)
    {
        Hotkey = PrepareHotkey("Mute/Un-mute microphone", x => x.Hotkey, (config, hotkeyConfig) => config.Hotkey = hotkeyConfig);
        HotkeyToggle = PrepareHotkey("Toggle microphone state", x => x.HotkeyForToggle, (config, hotkeyConfig) => config.HotkeyForToggle = hotkeyConfig);
        HotkeyMute = PrepareHotkey("Mute microphone", x => x.HotkeyForMute, (config, hotkeyConfig) => config.HotkeyForMute = hotkeyConfig);
        HotkeyUnmute = PrepareHotkey("Un-mute microphone", x => x.HotkeyForUnmute, (config, hotkeyConfig) => config.HotkeyForUnmute = hotkeyConfig);
        HotkeyPushToMute = PrepareHotkey("Push-To-Mute", x => x.HotkeyForPushToMute, (config, hotkeyConfig) => config.HotkeyForPushToMute = hotkeyConfig);
        HotkeyPushToTalk = PrepareHotkey("Push-To-Talk", x => x.HotkeyForPushToTalk, (config, hotkeyConfig) => config.HotkeyForPushToTalk = hotkeyConfig);
         
        PrepareTracker(HotkeyMode.Click, HotkeyToggle)
            .ObservableForProperty(x => x.IsActive, skipInitial: true)
            .SubscribeSafe(x =>
            {
                Log.Debug($"[{x.Sender}] Toggling microphone state: {Controller}");
                Controller.Mute = !Controller.Mute;
            }, Log.HandleUiException)
            .AddTo(Anchors);

        PrepareTracker(HotkeyMode.Hold, HotkeyMute)
            .ObservableForProperty(x => x.IsActive, skipInitial: true)
            .Where(x => x.Value)
            .SubscribeSafe(x =>
            {
                Log.Debug($"[{x.Sender}] Muting microphone: {Controller}");
                Controller.Mute = true;
            }, Log.HandleUiException)
            .AddTo(Anchors);

        PrepareTracker(HotkeyMode.Hold, HotkeyUnmute)
            .ObservableForProperty(x => x.IsActive, skipInitial: true)
            .Where(x => x.Value)
            .SubscribeSafe(x =>
            {
                Log.Debug($"[{x.Sender}] Un-muting microphone: {Controller}");
                Controller.Mute = false;
            }, Log.HandleUiException)
            .AddTo(Anchors);

        PrepareTracker(HotkeyMode.Hold, HotkeyPushToTalk)
            .ObservableForProperty(x => x.IsActive, skipInitial: true)
            .SubscribeSafe(x =>
            {
                Log.Debug($"[{x.Sender}] Processing push-to-talk hotkey for microphone: {Controller}");
                Controller.Mute = !x.Value;
            }, Log.HandleUiException)
            .AddTo(Anchors);

        PrepareTracker(HotkeyMode.Hold, HotkeyPushToMute)
            .ObservableForProperty(x => x.IsActive, skipInitial: true)
            .SubscribeSafe(x =>
            {
                Log.Debug($"[{x.Sender}] Processing push-to-mute hotkey for microphone: {Controller}");
                Controller.Mute = x.Value;
            }, Log.HandleUiException)
            .AddTo(Anchors);

        hotkeyConfigProvider.ListenTo(x => x.MuteMode)
            .Subscribe(x =>
            {
                Log.Debug($"Mute mode loaded from config: {x}");
                MuteMode = x;
            })
            .AddTo(Anchors);

        hotkeyConfigProvider.ListenTo(x => x.EnableAdvancedHotkeys)
            .Subscribe(x => IsEnabled = x)
            .AddTo(Anchors);

        hotkeyConfigProvider.ListenTo(x => x.InitialMicrophoneState)
            .Subscribe(x => InitialMicrophoneState = x)
            .AddTo(Anchors);

        configProvider.ListenTo(x => x.VolumeControlEnabled)
            .SubscribeSafe(x => VolumeControlIsEnabled = x, Log.HandleException)
            .AddTo(Anchors);

        Observable.Merge(
                configProvider.ListenTo(x => x.MicrophoneLineId).ToUnit(),
                Devices.ToObservableChangeSet().ToUnit())
            .Select(_ => configProvider.ActualConfig.MicrophoneLineId)
            .SubscribeSafe(configLineId =>
            {
                Log.Debug($"Microphone line configuration changed, lineId: {configLineId}, known lines: {Devices.Dump()}");

                var micLine = Devices.FirstOrDefault(line => line.Equals(configLineId));
                if (micLine.IsEmpty)
                {
                    Log.Debug($"Selecting first one of available microphone lines, known lines: {Devices.Dump()}");
                    micLine = Devices.FirstOrDefault();
                }

                DeviceId = micLine;
                MuteMicrophoneCommand.ResetError();
            }, Log.HandleUiException)
            .AddTo(Anchors);

        this.WhenAnyValue(x => x.MuteMode, x => x.InitialMicrophoneState)
            .SubscribeSafe(_ =>
            {
                Log.Debug($"Processing muteMode: {MuteMode}, {Controller}.Mute: {Controller.Mute}");
                switch (MuteMode)
                {
                    case MuteMode.PushToTalk:
                        Log.Debug($"{MuteMode} mute mode is enabled, un-muting microphone");
                        Controller.Mute = true;
                        break;
                    case MuteMode.PushToMute:
                        Controller.Mute = false;
                        Log.Debug($"{MuteMode} mute mode is enabled, muting microphone");
                        break;
                    case MuteMode.ToggleMute when InitialMicrophoneState == MicrophoneState.Mute:
                        Log.Debug($"{MuteMode} enabled, muting microphone");
                        Controller.Mute = true;
                        break;
                    case MuteMode.ToggleMute when InitialMicrophoneState == MicrophoneState.Unmute:
                        Log.Debug($"{MuteMode} enabled, un-muting microphone");
                        Controller.Mute = false;
                        break;
                    default:
                        Log.Debug($"{MuteMode} enabled, action is not needed");
                        break;
                }
            }, Log.HandleUiException)
            .AddTo(Anchors);

        hotkeyTracker
            .WhenAnyValue(x => x.IsActive)
            .Skip(1)
            .SubscribeSafe(async isActive =>
            {
                Log.Debug($"Handling hotkey press (isActive: {isActive}), mute mode: {MuteMode}");
                switch (MuteMode)
                {
                    case MuteMode.PushToTalk:
                        Controller.Mute = !isActive;
                        break;
                    case MuteMode.PushToMute:
                        Controller.Mute = isActive;
                        break;
                    case MuteMode.ToggleMute:
                        Controller.Mute = !Controller.Mute;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(MuteMode), MuteMode, @"Unsupported mute mode");
                }
            }, Log.HandleUiException)
            .AddTo(Anchors);

        Observable.Merge(
                this.ObservableForProperty(x => x.MuteMode, skipInitial: true).ToUnit(),
                this.ObservableForProperty(x => x.IsEnabled, skipInitial: true).ToUnit(),
                this.ObservableForProperty(x => x.InitialMicrophoneState, skipInitial: true).ToUnit(),
                Hotkey.ObservableForProperty(x => x.Properties, skipInitial: true).ToUnit())
            .Throttle(ConfigThrottlingTimeout)
            .SubscribeSafe(() =>
            {
                var hotkeyConfig = hotkeyConfigProvider.ActualConfig.CloneJson();
                hotkeyConfig.Hotkey = Hotkey.Properties;
                hotkeyConfig.MuteMode = MuteMode;
                hotkeyConfig.EnableAdvancedHotkeys = IsEnabled;
                hotkeyConfig.InitialMicrophoneState = InitialMicrophoneState;
                hotkeyConfigProvider.Save(hotkeyConfig);
            }, Log.HandleUiException)
            .AddTo(Anchors);

        Observable.Merge(
                this.ObservableForProperty(x => x.DeviceId, skipInitial: true).ToUnit(),
                this.ObservableForProperty(x => x.Volume, skipInitial: true).ToUnit(),
                this.ObservableForProperty(x => x.VolumeControlIsEnabled, skipInitial: true).ToUnit())
            .Throttle(ConfigThrottlingTimeout)
            .SubscribeSafe(() =>
            {
                var config = configProvider.ActualConfig.CloneJson();
                config.MicrophoneLineId = DeviceId;
                config.VolumeControlEnabled = VolumeControlIsEnabled;
                config.Volume = VolumeControlIsEnabled ? Volume : null;
                configProvider.Save(config);
            }, Log.HandleUiException)
            .AddTo(Anchors);

        if (configProvider.ActualConfig.VolumeControlEnabled && configProvider.ActualConfig.Volume != null)
        {
            Log.Debug(() => $"Setting initial Volume of {Controller} to {configProvider.ActualConfig.Volume}");
            Controller.Volume = configProvider.ActualConfig.Volume;
        }

        Controller.WhenAnyValue(x => x.ActiveController)
            .WithPrevious()
            .Subscribe(x =>
            {
                if (x.Current.DeviceId.LineId != MMDeviceId.All.LineId || x.Previous == null)
                {
                    return;
                }

                if (VolumeControlIsEnabled)
                {
                    Log.Debug(() => $"Propagating volume from previous controller {x.Previous}: {x.Previous.Volume}");
                    x.Current.Volume = x.Previous.Volume;
                }
                
                Log.Debug(() => $"Propagating Mute state from previous controller {x.Previous}: {x.Previous.Mute}");
                x.Current.Mute = x.Previous.Mute;
            })
            .AddTo(Anchors);
        
        Binder.Attach(this).AddTo(Anchors);
    }

    public MuteMode MuteMode { get; set; }

    public IHotkeyEditorViewModel Hotkey { get; }

    public IHotkeyEditorViewModel HotkeyToggle { get; }

    public IHotkeyEditorViewModel HotkeyMute { get; }

    public IHotkeyEditorViewModel HotkeyUnmute { get; }

    public IHotkeyEditorViewModel HotkeyPushToTalk { get; }

    public IHotkeyEditorViewModel HotkeyPushToMute { get; }

    public MicrophoneState InitialMicrophoneState { get; set; }
}