using MicSwitch.Modularity;
using MicSwitch.Services;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class OutputControllerViewModel : MediaControllerBase<MicSwitchVolumeControlConfig>, IOutputControllerViewModel
    {
        private static readonly Binder<OutputControllerViewModel> Binder = new();

        static OutputControllerViewModel()
        {
            Binder.Bind(x => x.IsEnabled).To(x => x.VolumeControlIsEnabled);
        }

        public OutputControllerViewModel(
            IMMRenderDeviceProvider deviceProvider,
            IFactory<IMMDeviceControllerEx, IMMDeviceProvider> deviceControllerFactory,
            IFactory<IHotkeyTracker> hotkeyTrackerFactory,
            IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory,
            IConfigProvider<MicSwitchVolumeControlConfig> hotkeyConfigProvider,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler) : base(deviceProvider, deviceControllerFactory.Create(deviceProvider), hotkeyTrackerFactory, hotkeyEditorFactory, hotkeyConfigProvider, uiScheduler)
        {
            HotkeyToggleMute = PrepareHotkey("Mute/Un-mute", x => x.HotkeyForToggle, (config, hotkeyConfig) => config.HotkeyForToggle = hotkeyConfig);
            HotkeyMute = PrepareHotkey("Mute", x => x.HotkeyForMute, (config, hotkeyConfig) => config.HotkeyForMute = hotkeyConfig);
            HotkeyUnmute = PrepareHotkey("Un-mute", x => x.HotkeyForUnmute, (config, hotkeyConfig) => config.HotkeyForUnmute = hotkeyConfig);
            HotkeyVolumeDown = PrepareHotkey("Volume Down", x => x.HotkeyForVolumeDown, (config, hotkeyConfig) => config.HotkeyForVolumeDown = hotkeyConfig);
            HotkeyVolumeUp = PrepareHotkey("Volume Up", x => x.HotkeyForVolumeUp, (config, hotkeyConfig) => config.HotkeyForVolumeUp = hotkeyConfig);

            hotkeyConfigProvider.ListenTo(x => x.IsEnabled)
                .ObserveOn(uiScheduler)
                .Subscribe(x => IsEnabled = x)
                .AddTo(Anchors);

            PrepareTracker(HotkeyMode.Click, HotkeyToggleMute)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Toggling state: {Controller}");
                    Controller.Mute = !Controller.Mute;
                }, Log.HandleUiException)
                .AddTo(Anchors);

            PrepareTracker(HotkeyMode.Hold, HotkeyMute)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .Where(x => x.Value)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Muting: {Controller}");
                    Controller.Mute = true;
                }, Log.HandleUiException)
                .AddTo(Anchors);

            PrepareTracker(HotkeyMode.Hold, HotkeyUnmute)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .Where(x => x.Value)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Un-muting: {Controller}");
                    Controller.Mute = false;
                }, Log.HandleUiException)
                .AddTo(Anchors);

            PrepareTracker(HotkeyMode.Hold, HotkeyVolumeUp)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .SwitchIf(x => x.Value == true, x => Observable.Interval(TimeSpan.FromMilliseconds(10)), x => Observable.Empty<long>())
                .SubscribeSafe(x =>
                {
                    if (Controller.Volume == null)
                    {
                        return;
                    }

                    Controller.Volume = (float)Math.Min(1, Controller.Volume.Value + 0.01);
                }, Log.HandleUiException)
                .AddTo(Anchors);

            PrepareTracker(HotkeyMode.Hold, HotkeyVolumeDown)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .SwitchIf(x => x.Value == true, x => Observable.Interval(TimeSpan.FromMilliseconds(10)), x => Observable.Empty<long>())
                .SubscribeSafe(x =>
                {
                    if (Controller.Volume == null)
                    {
                        return;
                    }

                    Controller.Volume = (float)Math.Max(0, Controller.Volume.Value - 0.01);
                }, Log.HandleUiException)
                .AddTo(Anchors);


            Observable.Merge(
                    hotkeyConfigProvider.ListenTo(x => x.DeviceId).ToUnit(),
                    Devices.ToObservableChangeSet().ToUnit())
                .Select(_ => hotkeyConfigProvider.ActualConfig.DeviceId)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(configLineId =>
                {
                    Log.Debug($"Device line configuration changed, lineId: {configLineId}, known lines: {Devices.Dump()}");

                    var line = Devices.FirstOrDefault(line => line.Equals(configLineId));
                    if (line.IsEmpty)
                    {
                        Log.Debug($"Selecting first one of available microphone lines, known lines: {Devices.Dump()}");
                        line = Devices.FirstOrDefault();
                    }

                    DeviceId = line;
                    MuteCommand.ResetError();
                }, Log.HandleUiException)
                .AddTo(Anchors);

            Observable.Merge(
                    this.ObservableForProperty(x => x.DeviceId, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsEnabled, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() =>
                {
                    var hotkeyConfig = hotkeyConfigProvider.ActualConfig.CloneJson();
                    hotkeyConfig.IsEnabled = IsEnabled;
                    hotkeyConfig.DeviceId = DeviceId;
                    hotkeyConfigProvider.Save(hotkeyConfig);
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            Binder.Attach(this).AddTo(Anchors);
        }

        public IHotkeyEditorViewModel HotkeyToggleMute { get; }
        public IHotkeyEditorViewModel HotkeyMute { get; }
        public IHotkeyEditorViewModel HotkeyUnmute { get; }

        public IHotkeyEditorViewModel HotkeyVolumeUp { get; }

        public IHotkeyEditorViewModel HotkeyVolumeDown { get; }
    }
}