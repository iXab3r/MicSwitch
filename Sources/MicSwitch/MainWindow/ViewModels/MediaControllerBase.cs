using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Services;
using NAudio.CoreAudioApi;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels;

internal abstract class MediaControllerBase<TConfig> : DisposableReactiveObjectWithLogger, IMediaController where TConfig : IPoeEyeConfig
{
    protected static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly Binder<MediaControllerBase<TConfig>> Binder = new();

    private readonly IMMDeviceControllerEx deviceController;
    private readonly IFactory<IHotkeyTracker> hotkeyTrackerFactory;
    private readonly IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory;
    private readonly IConfigProvider<TConfig> hotkeyConfigProvider;
    private readonly IScheduler uiScheduler;

    static MediaControllerBase()
    {
        Binder.Bind(x => x.deviceController.VolumePercent).To((x, v) => x.Volume = v ?? 0);
        Binder.Bind(x => x.deviceController.Mute).To((x, v) => x.Mute = v ?? false);
        Binder.BindIf(x => x.IsEnabled, x => (double?)x.Volume).To(x => x.deviceController.VolumePercent);
        Binder.Bind(x => x.DeviceId).To(x => x.deviceController.LineId);
    }

    protected MediaControllerBase(
        IMMDeviceProvider deviceProvider,
        IMMDeviceControllerEx deviceController,
        IFactory<IHotkeyTracker> hotkeyTrackerFactory,
        IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory,
        IConfigProvider<TConfig> hotkeyConfigProvider,
        [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
    {
        deviceProvider.Devices
            .ToObservableChangeSet()
            .ObserveOn(uiScheduler)
            .BindToCollection(out var microphones)
            .SubscribeToErrors(Log.HandleUiException)
            .AddTo(Anchors);
        Devices = microphones;
        
        Controller = deviceController.AddTo(Anchors);
        this.deviceController = deviceController;
        this.hotkeyTrackerFactory = hotkeyTrackerFactory;
        this.hotkeyEditorFactory = hotkeyEditorFactory;
        this.hotkeyConfigProvider = hotkeyConfigProvider;
        this.uiScheduler = uiScheduler;
        MuteMicrophoneCommand = CommandWrapper.Create<object>(MuteMicrophoneCommandExecuted);
        Binder.Attach(this).AddTo(Anchors);
    }
    
    public IReadOnlyObservableCollection<MMDeviceId> Devices { get; }

    public MMDeviceId DeviceId { get; set; }
    
    public bool IsEnabled { get; set; }
    
    public IMMDeviceControllerEx Controller { get; }
    
    public CommandWrapper MuteMicrophoneCommand { get; }
    
    public bool Mute { get; [UsedImplicitly] private set; }

    public double Volume { get; set; }
    
    protected IHotkeyEditorViewModel PrepareHotkey(
        string description,
        Expression<Func<TConfig, HotkeyConfig>> fieldToMonitor,
        Action<TConfig, HotkeyConfig> consumer)
    {
        return PrepareHotkey(
            this,
            description,
            fieldToMonitor,
            consumer).AddTo(Anchors);
    }

    protected IHotkeyTracker PrepareTracker(
        HotkeyMode hotkeyMode,
        IHotkeyEditorViewModel hotkeyEditor)
    {
        return PrepareTracker(
            this,
            hotkeyMode,
            hotkeyEditor);
    }

    private static IHotkeyTracker PrepareTracker(
        MediaControllerBase<TConfig> owner,
        HotkeyMode hotkeyMode,
        IHotkeyEditorViewModel hotkeyEditor)
    {
        var result = owner.hotkeyTrackerFactory.Create();
        result.HotkeyMode = hotkeyMode;
        Observable.Merge(
                owner.WhenAnyValue(x => x.IsEnabled).ToUnit(),
                hotkeyEditor.WhenAnyValue(x => x.Key, x => x.AlternativeKey, x => x.SuppressKey).ToUnit())
            .SubscribeSafe(x =>
            {
                result.SuppressKey = hotkeyEditor.SuppressKey;
                result.IgnoreModifiers = hotkeyEditor.IgnoreModifiers;
                result.HandleApplicationKeys = true;

                result.Clear();
                if (!owner.IsEnabled)
                {
                    return;
                }

                if (hotkeyEditor.Key != null)
                {
                    result.Add(hotkeyEditor.Key);
                }

                if (hotkeyEditor.AlternativeKey != null)
                {
                    result.Add(hotkeyEditor.AlternativeKey);
                }

                if (result.Hotkeys.Any())
                {
                    owner.IsEnabled = true;
                }
            }, owner.Log.HandleUiException)
            .AddTo(owner.Anchors);
        return result.AddTo(owner.Anchors);
    }

    private static IHotkeyEditorViewModel PrepareHotkey(
        MediaControllerBase<TConfig> owner,
        string description,
        Expression<Func<TConfig, HotkeyConfig>> fieldToMonitor,
        Action<TConfig, HotkeyConfig> consumer)
    {
        var result = owner.hotkeyEditorFactory.Create();

        owner.hotkeyConfigProvider.ListenTo(fieldToMonitor)
            .Select(x => x ?? HotkeyConfig.Empty)
            .ObserveOn(owner.uiScheduler)
            .SubscribeSafe(config =>
            {
                owner.Log.Debug($"Setting new hotkeys configuration: {config.Dump()}, current: {result.Properties}");
                result.Load(config);
            }, owner.Log.HandleException)
            .AddTo(owner.Anchors);

        result.ObservableForProperty(x => x.Properties, skipInitial: true)
            .Throttle(ConfigThrottlingTimeout)
            .SubscribeSafe(() =>
            {
                var hotkeyConfig = owner.hotkeyConfigProvider.ActualConfig.CloneJson();
                consumer(hotkeyConfig, result.Properties);
                owner.hotkeyConfigProvider.Save(hotkeyConfig);
            }, owner.Log.HandleUiException)
            .AddTo(owner.Anchors);

        result.Description = description;
        
        return result;
    }

    private async Task MuteMicrophoneCommandExecuted(object arg)
    {
        var mute = arg switch
        {
            bool argBool => argBool,
            _ => !deviceController.Mute
        };
        Log.Debug($"{(mute == true ? "Muting" : "Un-muting")} microphone {deviceController.LineId}");
        deviceController.Mute = mute;
    }
}