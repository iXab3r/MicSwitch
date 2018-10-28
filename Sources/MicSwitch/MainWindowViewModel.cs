using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using JetBrains.Annotations;
using PoeShared.Native;
using PoeShared.Scaffolding;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Common.Logging;
using DynamicData.Binding;
using MahApps.Metro.Controls;
using MicSwitch.WPF.Hotkeys;
using NAudio.Mixer;
using PoeEye;
using PoeShared;
using PoeShared.Audio;
using PoeShared.Modularity;
using PoeShared.Prism;
using PoeShared.Scaffolding.WPF;
using Prism.Commands;
using ReactiveUI;
using Unity.Attributes;
using Application = System.Windows.Application;

namespace MicSwitch
{
    internal class MainWindowViewModel : DisposableReactiveObject
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private static readonly string ExplorerExecutablePath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe");

        private readonly IMicrophoneController microphoneController;
        private readonly IWindowTracker mainWindowTracker;
        
        private MicrophoneLineData microphoneLine;
        private bool isPushToTalkMode;
        private WindowState windowState;
        private Visibility trayIconVisibility;
        private bool showInTaskbar;
        private HotkeyGesture hotkey;

        public MainWindowViewModel(
            [NotNull] IKeyboardEventsSource eventSource,
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IMicSwitchOverlayViewModel overlay,
            [NotNull] IAudioNotificationsManager audioNotificationsManager,
            [NotNull] [Dependency(WellKnownWindows.MainWindow)] IWindowTracker mainWindowTracker,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider)
        {
            this.microphoneController = microphoneController;
            
            this.mainWindowTracker = mainWindowTracker;
            this.BindPropertyTo(x => x.IsActive, mainWindowTracker, x => x.IsActive).AddTo(Anchors);
            
            Overlay = overlay;
            
            this.BindPropertyTo(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent).AddTo(Anchors);
            this.BindPropertyTo(x => x.MicrophoneMuted, microphoneController, x => x.Mute).AddTo(Anchors);
            
            Microphones = new ReadOnlyObservableCollection<MicrophoneLineData>(
                new ObservableCollection<MicrophoneLineData>(new MicrophoneProvider().EnumerateLines())
            );

            this.ObservableForProperty(x => x.MicrophoneMuted, skipInitial:true)
                .DistinctUntilChanged()
                .Where(x => MicrophoneLine != null)
                .Skip(1) // skip initial setup
                .Subscribe(x => { audioNotificationsManager.PlayNotification(x.Value ? "beep300" : "beep750"); })
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.MicrophoneLine)
                .DistinctUntilChanged()
                .Where(x => MicrophoneLine != null)
                .Subscribe(x => microphoneController.LineId = x)
                .AddTo(Anchors);

            Observable.Merge(
                    configProvider.ListenTo(x => x.MicrophoneLineId).ToUnit(),
                    Microphones.ToObservableChangeSet().ToUnit()
                    )
                .Select(x => configProvider.ActualConfig.MicrophoneLineId)
                .Where(x => x != null && !x.Equals(MicrophoneLine))
                .Select(x => Microphones.FirstOrDefault(line => line.Equals(x)))
                .Subscribe(x =>
                {
                    MicrophoneLine = x;
                })
                .AddTo(Anchors);

            configProvider.ListenTo(x => x.MicrophoneHotkey)
                .Where(x => x != null)
                .Select(x => (HotkeyGesture)new HotkeyConverter().ConvertFrom(x))
                .DistinctUntilChanged()
                .ObserveOnDispatcher()
                .Select(configHotkey =>
                {
                    Log.Info($"New hotkey assigned: {configHotkey}");
                    if (!configHotkey.Equals(this.hotkey))
                    {
                        Log.Debug($"Syncing config hotkey with UI hotkey, {Hotkey} (UI) becomes {configHotkey}(config)");
                        Hotkey = configHotkey;
                    }
                    
                    var hotkeyDown = 
                        Observable.Merge(
                            eventSource.WhenMouseDown.Select(x => new HotkeyGesture(x.Button)),
                            eventSource.WhenKeyDown.Select(x => new HotkeyGesture(x.KeyCode.ToInputKey(), x.Modifiers.ToModifiers())))
                        .Where(x => configHotkey.Equals(x))
                        .Select(x => new { KeyDown = true });
                    var hotkeyUp = 
                        Observable.Merge(
                            eventSource.WhenMouseUp.Select(x => new HotkeyGesture(x.Button)),
                            eventSource.WhenKeyUp.Select(x => new HotkeyGesture(x.KeyCode.ToInputKey(), x.Modifiers.ToModifiers())))
                         .Where(x => configHotkey.Equals(x))
                         .Select(x => new { KeyDown = false });

                    return Observable.Merge(hotkeyDown, hotkeyUp)
                        .Where(x =>
                        {
                            if (!mainWindowTracker.IsActive)
                            {
                                Log.Trace($"Main window is NOT active, processing hotkey {configHotkey} (isDown: {x})");
                                return true;
                            }
                            else
                            {
                                Log.Trace($"Main window is active, skipping hotkey {configHotkey} (isDown: {x})");
                                return false;
                            }
                        });
                })
                .Switch()
                .Subscribe(keyInfo =>
                {
                    Log.Debug($"Hotkey pressed, state: {(keyInfo.KeyDown ? "down" : "up")}");

                    if (!isPushToTalkMode)
                    {
                        if (keyInfo.KeyDown)
                        {
                            MicrophoneMuted = !MicrophoneMuted;
                        }
                    }
                    else
                    {
                        MicrophoneMuted = !keyInfo.KeyDown;
                    }
                }, Log.HandleException)
                .AddTo(Anchors);
            
            ToggleOverlayLockCommand = new DelegateCommand(() =>
            {
                if (overlay.IsLocked && overlay.UnlockWindowCommand.CanExecute(null))
                {
                    overlay.UnlockWindowCommand.Execute(null);
                } else if (!overlay.IsLocked && overlay.LockWindowCommand.CanExecute(null))
                {
                    overlay.LockWindowCommand.Execute(null);
                }
            });
            
            ExitAppCommand = new DelegateCommand(() =>
            {
                Log.Debug($"Closing application");
                configProvider.Save(configProvider.ActualConfig);
                Application.Current.Shutdown();
            });

            this.WhenAnyValue(x => x.WindowState)
                .Subscribe(x => ShowInTaskbar = x != WindowState.Minimized)
                .AddTo(Anchors);
            
            ShowAppCommand = new DelegateCommand(() => { WindowState = WindowState.Normal; });
            
            OpenAppDataDirectoryCommand = CommandWrapper.Create(OpenAppDataDirectory);

            var executingAssembly = Assembly.GetExecutingAssembly();
            Title = $"{(AppArguments.Instance.IsDebugMode ? "[D]" : "")} {executingAssembly.GetName().Name} v{executingAssembly.GetName().Version}";
            
            // config processing
            Observable.Merge(
                    this.ObservableForProperty(x => x.MicrophoneLine, skipInitial:true).ToUnit(),
                    this.ObservableForProperty(x => x.IsPushToTalkMode, skipInitial:true).ToUnit(),
                    this.ObservableForProperty(x => x.Hotkey, skipInitial:true).ToUnit())
                .Subscribe(() =>
                {
                    var config = configProvider.ActualConfig.CloneJson();
                    config.IsPushToTalkMode = IsPushToTalkMode;
                    config.MicrophoneHotkey = (Hotkey ?? new HotkeyGesture()).ToString();
                    config.MicrophoneLineId = MicrophoneLine;
                    configProvider.Save(config);
                })
                .AddTo(Anchors);
        }

        public ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }
        
        public ICommand ToggleOverlayLockCommand { get; }
        
        public ICommand ExitAppCommand { get; }
        
        public ICommand ShowAppCommand { get; }
        
        public CommandWrapper OpenAppDataDirectoryCommand { get; }
        
        public IMicSwitchOverlayViewModel Overlay { get; }

        public bool IsActive => mainWindowTracker.IsActive;

        public WindowState WindowState
        {
            get => windowState;
            set => this.RaiseAndSetIfChanged(ref windowState, value);
        }

        public Visibility TrayIconVisibility
        {
            get => trayIconVisibility;
            set => this.RaiseAndSetIfChanged(ref trayIconVisibility, value);
        }

        public bool ShowInTaskbar
        {
            get => showInTaskbar;
            set => this.RaiseAndSetIfChanged(ref showInTaskbar, value);
        }

        public HotkeyGesture Hotkey
        {
            get => hotkey;
            set => this.RaiseAndSetIfChanged(ref hotkey, value);
        }
        
        public string Title { get; }

        public bool IsPushToTalkMode
        {
            get => isPushToTalkMode;
            set => this.RaiseAndSetIfChanged(ref isPushToTalkMode, value);
        }

        public MicrophoneLineData MicrophoneLine
        {
            get => microphoneLine;
            set => this.RaiseAndSetIfChanged(ref microphoneLine, value);
        }

        public double MicrophoneVolume
        {
            get => microphoneController.VolumePercent ?? 0;
            set => microphoneController.VolumePercent = value;
        }

        public bool MicrophoneMuted
        {
            get => microphoneController.Mute ?? false;
            set => microphoneController.Mute = value;
        }
        
        private async Task OpenAppDataDirectory()
        {
            await Task.Run(() => Process.Start(ExplorerExecutablePath, AppArguments.AppDomainDirectory));
        }
    }
}