using log4net;
using MicSwitch.Modularity;
using MicSwitch.Services;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class OutputControllerViewModel : MediaControllerBase, IOutputControllerViewModel
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
        }
        
        public IHotkeyEditorViewModel HotkeyOutputMute { get; }

        public IHotkeyEditorViewModel HotkeyOutputVolumeUp { get; }

        public IHotkeyEditorViewModel HotkeyOutputVolumeDown { get; }
    }
}