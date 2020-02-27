using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Windows;
using JetBrains.Annotations;
using log4net;
using MicSwitch.Modularity;
using Mono.Collections.Generic;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.UI.Hotkeys;
using ReactiveUI;
using Unity;

namespace MicSwitch.Services
{
    internal sealed class ComplexHotkeyTracker : DisposableReactiveObject, IComplexHotkeyTracker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ComplexHotkeyTracker));

        private readonly IHotkeyConverter hotkeyConverter;
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IFactory<IHotkeyTracker> hotkeyTrackerFactory;
        private readonly Collection<IHotkeyTracker> trackers = new Collection<IHotkeyTracker>();
        private bool isActive;
        private HookForm hookForm;

        public ComplexHotkeyTracker(
            [NotNull] IHotkeyConverter hotkeyConverter,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] IFactory<IHotkeyTracker> hotkeyTrackerFactory)
        {
            this.hotkeyConverter = hotkeyConverter;
            this.configProvider = configProvider;
            this.hotkeyTrackerFactory = hotkeyTrackerFactory;
            Log.Debug($"Scheduling HotkeyTracker initialization using background scheduler");
            
            var bgThread = new Thread(x => Initialize())
            {
                IsBackground = true, 
                ApartmentState = ApartmentState.STA, 
                Name = "HotkeyTracker"
            };
            bgThread.Start();
        }

        public bool IsActive
        {
            get => isActive;
            private set => this.RaiseAndSetIfChanged(ref isActive, value);
        }

        private void Initialize()
        {
            Log.Debug($"Initializing HotkeyTracker");

            var hotkey = hotkeyTrackerFactory.Create();
            var hotkeyAlt = hotkeyTrackerFactory.Create();
            trackers.Add(hotkey);
            trackers.Add(hotkeyAlt);

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

            foreach (var tracker in trackers)
            {
                tracker
                    .WhenAnyValue(x => x.IsActive)
                    .Subscribe(x => IsActive = !IsActive)
                    .AddTo(Anchors);
            }

            try
            {
                Log.Debug($"Running message loop");
                hookForm = new HookForm();
                hookForm.ShowDialog();
            }
            catch (Exception e)
            {
                Log.HandleUiException(new ApplicationException("Exception occurred in Complex Hotkey message loop", e));
            }
            finally
            {
                Log.Debug($"Message loop terminated");
            }
        }

        private class HookForm : TransparentWindow
        {
            public HookForm()
            {
                ShowInTaskbar = false;
                WindowStyle = WindowStyle.None;
                Width = 0;
                Height = 0;
                Visibility = Visibility.Collapsed;
                MakeTransparent();
            }
        }
    }
}