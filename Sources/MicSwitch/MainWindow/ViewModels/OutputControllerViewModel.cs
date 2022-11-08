using log4net;
using MicSwitch.Modularity;
using MicSwitch.Services;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class OutputControllerViewModel : MediaControllerBase<MicSwitchHotkeyConfig>, IOutputControllerViewModel
    {
        public OutputControllerViewModel(
            IMMRenderDeviceProvider deviceProvider,
            IFactory<IMMDeviceControllerEx, IMMDeviceProvider> deviceControllerFactory,
            IFactory<IHotkeyTracker> hotkeyTrackerFactory,
            IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory,
            IConfigProvider<MicSwitchHotkeyConfig> hotkeyConfigProvider,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler) : base(deviceProvider, deviceControllerFactory.Create(deviceProvider), hotkeyTrackerFactory, hotkeyEditorFactory, hotkeyConfigProvider, uiScheduler)
        {
            HotkeyOutputMute = PrepareHotkey("Mute/Un-mute", x => x.HotkeyForOutputMute, (config, hotkeyConfig) => config.HotkeyForOutputMute = hotkeyConfig);
            HotkeyOutputVolumeDown = PrepareHotkey("Volume Down", x => x.HotkeyForOutputVolumeDown, (config, hotkeyConfig) => config.HotkeyForOutputVolumeDown = hotkeyConfig);
            HotkeyOutputVolumeUp = PrepareHotkey("Volume Up", x => x.HotkeyForOutputVolumeUp, (config, hotkeyConfig) => config.HotkeyForOutputVolumeUp = hotkeyConfig);
            
            hotkeyConfigProvider.ListenTo(x => x.EnableOutputVolumeControl)
                .ObserveOn(uiScheduler)
                .Subscribe(x => IsEnabled = x)
                .AddTo(Anchors);
            
            Observable.Merge(
                    this.ObservableForProperty(x => x.IsEnabled, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() =>
                {
                    var hotkeyConfig = hotkeyConfigProvider.ActualConfig.CloneJson();
                    hotkeyConfig.EnableOutputVolumeControl = IsEnabled;
                    hotkeyConfigProvider.Save(hotkeyConfig);
                }, Log.HandleUiException)
                .AddTo(Anchors);
        }
        
        public IHotkeyEditorViewModel HotkeyOutputMute { get; }

        public IHotkeyEditorViewModel HotkeyOutputVolumeUp { get; }

        public IHotkeyEditorViewModel HotkeyOutputVolumeDown { get; }
    }
}