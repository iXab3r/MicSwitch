using System;
using System.Linq;
using JetBrains.Annotations;
using MicSwitch.Modularity;
using PoeShared.Modularity;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.UI.Hotkeys;
using ReactiveUI;

namespace MicSwitch.Services
{
    internal sealed class ComplexHotkeyTracker : DisposableReactiveObject, IComplexHotkeyTracker
    {
        private readonly IHotkeyTracker[] trackers;
        private bool isActive;

        public ComplexHotkeyTracker(
            [NotNull] IHotkeyConverter hotkeyConverter,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] IFactory<IHotkeyTracker> hotkeyTrackerFactory)
        {
            var hotkey = hotkeyTrackerFactory.Create();
            var hotkeyAlt = hotkeyTrackerFactory.Create();
            trackers = new[] { hotkey, hotkeyAlt };

            configProvider.WhenChanged
                .Subscribe(
                    () =>
                    {
                        var actualConfig = configProvider.ActualConfig;
                        hotkey.Hotkey = hotkeyConverter.ConvertFromString(actualConfig.MicrophoneHotkey);
                        hotkeyAlt.Hotkey = hotkeyConverter.ConvertFromString(actualConfig.MicrophoneHotkeyAlt);

                        hotkey.HotkeyMode = hotkeyAlt.HotkeyMode = actualConfig.IsPushToTalkMode
                            ? HotkeyMode.Hold
                            : HotkeyMode.Click;
                        hotkey.SuppressKey = hotkeyAlt.SuppressKey = actualConfig.SuppressHotkey;
                    })
                .AddTo(Anchors);

            hotkey
                .WhenAnyValue(x => x.IsActive)
                .Subscribe(x => IsActive = trackers.Any(y => y.IsActive))
                .AddTo(Anchors);
        }

        public bool IsActive
        {
            get => isActive;
            private set => this.RaiseAndSetIfChanged(ref isActive, value);
        }
    }
}