using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Common.Logging;
using DynamicData.Binding;
using JetBrains.Annotations;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Updater;
using MicSwitch.WPF.Hotkeys;
using PoeEye;
using PoeShared;
using PoeShared.Audio.Services;
using PoeShared.Audio.ViewModels;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;
using PoeShared.Services;
using Prism.Commands;
using ReactiveUI;
using Unity.Attributes;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MicSwitch.MainWindow.ViewModels
{
    internal class MainWindowViewModel : DisposableReactiveObject
    {
        private static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromSeconds(1);
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private static readonly string ExplorerExecutablePath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe");
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IWindowTracker mainWindowTracker;

        private readonly IStartupManager startupManager;
        private readonly IMicrophoneController microphoneController;

        private HotkeyGesture hotkey;
        private HotkeyGesture hotkeyAlt;
        private bool isPushToTalkMode;

        private MicrophoneLineData microphoneLine;
        private bool showInTaskbar;
        private Visibility trayIconVisibility;
        private WindowState windowState;

        public MainWindowViewModel(
            [NotNull] IKeyboardEventsSource eventSource,
            [NotNull] IFactory<IStartupManager, StartupManagerArgs> startupManagerFactory,
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IMicSwitchOverlayViewModel overlay,
            [NotNull] IAudioNotificationsManager audioNotificationsManager,
            [NotNull] IFactory<IAudioNotificationSelectorViewModel> audioSelectorFactory,
            [NotNull] ApplicationUpdaterViewModel appUpdater,
            [NotNull] [Dependency(WellKnownWindows.MainWindow)] IWindowTracker mainWindowTracker,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider)
        {
            var startupManagerArgs = new StartupManagerArgs
            {
                UniqueAppName = AppArguments.Instance.AppName,
            };
            if (AppArguments.Instance.IsDebugMode)
            {
                startupManagerArgs.ExecutablePath = System.Windows.Forms.Application.ExecutablePath;
                startupManagerArgs.CommandLineArgs = AppArguments.Instance.StartupArgs;
            }
            this.startupManager = startupManagerFactory.Create(startupManagerArgs);

            this.microphoneController = microphoneController;

            ApplicationUpdater = appUpdater;
            this.mainWindowTracker = mainWindowTracker;
            this.configProvider = configProvider;
            this.BindPropertyTo(x => x.IsActive, mainWindowTracker, x => x.IsActive).AddTo(Anchors);

            AudioSelectorWhenMuted = audioSelectorFactory.Create();
            AudioSelectorWhenUnmuted = audioSelectorFactory.Create();

            Observable.Merge(
                    AudioSelectorWhenMuted.ObservableForProperty(x => x.SelectedValue, skipInitial: true),
                    AudioSelectorWhenUnmuted.ObservableForProperty(x => x.SelectedValue, skipInitial: true))
                .Subscribe(() => this.RaisePropertyChanged(nameof(AudioNotification)), Log.HandleException)
                .AddTo(Anchors);

            configProvider.ListenTo(x => x.Notification)
                .Subscribe(cfg =>
                {
                    Log.Debug($"Applying new notification configuration: {cfg.DumpToTextRaw()} (current: {AudioNotification.DumpToTextRaw()})");
                    AudioNotification = cfg;
                }, Log.HandleException)
                .AddTo(Anchors);

            Observable.Merge(configProvider.ListenTo(x => x.MicrophoneHotkey), configProvider.ListenTo(x => x.MicrophoneHotkeyAlt))
                .Select(x => new
                {
                    Hotkey = (HotkeyGesture)new HotkeyConverter().ConvertFrom(configProvider.ActualConfig.MicrophoneHotkey ?? string.Empty), 
                    HotkeyAlt = (HotkeyGesture)new HotkeyConverter().ConvertFrom(configProvider.ActualConfig.MicrophoneHotkeyAlt ?? string.Empty), 
                })
                .ObserveOnDispatcher()
                .Subscribe(cfg =>
                {
                    Log.Debug($"Setting new hotkeys configuration: {cfg.DumpToTextRaw()} (current: {hotkey}, alt: {hotkeyAlt})");
                    Hotkey = cfg.Hotkey;
                    HotkeyAlt = cfg.HotkeyAlt;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            Overlay = overlay;

            this.BindPropertyTo(x => x.RunAtLogin, startupManager, x => x.IsRegistered).AddTo(Anchors);
            this.BindPropertyTo(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent).AddTo(Anchors);
            this.BindPropertyTo(x => x.MicrophoneMuted, microphoneController, x => x.Mute).AddTo(Anchors);

            Microphones = new ReadOnlyObservableCollection<MicrophoneLineData>(
                new ObservableCollection<MicrophoneLineData>(new MicrophoneProvider().EnumerateLines())
            );

            this.ObservableForProperty(x => x.MicrophoneMuted, skipInitial: true)
                .DistinctUntilChanged()
                .Where(x => !MicrophoneLine.IsEmpty)
                .Skip(1) // skip initial setup
                .Subscribe(x =>
                {
                    var cfg = configProvider.ActualConfig.Notification;
                    var notificationToPlay = x.Value ? cfg.On : cfg.Off;
                    Log.Debug($"Playing notification {notificationToPlay} (cfg: {cfg.DumpToTextRaw()})");
                    audioNotificationsManager.PlayNotification(notificationToPlay);
                }, Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.MicrophoneLine)
                .DistinctUntilChanged()
                .Where(x => !MicrophoneLine.IsEmpty)
                .Subscribe(x => microphoneController.LineId = x, Log.HandleException)
                .AddTo(Anchors);

            Observable.Merge(
                    configProvider.ListenTo(x => x.MicrophoneLineId).ToUnit(),
                    Microphones.ToObservableChangeSet().ToUnit())
                .Select(_ => configProvider.ActualConfig.MicrophoneLineId)
                .Subscribe(configLineId =>
                {
                    Log.Debug($"Microphone line configuration changed: {configLineId}, known lines: {Microphones.DumpToTextRaw()}");

                    var micLine = Microphones.FirstOrDefault(line => line.Equals(configLineId));
                    if (micLine.IsEmpty)
                    {
                        Log.Debug($"Selecting first one of available microphone lines, known lines: {Microphones.DumpToTextRaw()}");
                        micLine = Microphones.FirstOrDefault();
                    }

                    MicrophoneLine = micLine;
                }, Log.HandleException)
                .AddTo(Anchors);

            BuildHotkeySubscription(eventSource)
                .Where(x =>
                {
                    if (!mainWindowTracker.IsActive)
                    {
                        Log.Trace($"Main window is NOT active, processing hotkey {x.Key} (isDown: {x})");
                        return true;
                    }

                    Log.Trace($"Main window is active, skipping hotkey {x.Key} (isDown: {x})");
                    return false;
                })
                .ObserveOnDispatcher()
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
                }
                else if (!overlay.IsLocked && overlay.LockWindowCommand.CanExecute(null))
                {
                    overlay.LockWindowCommand.Execute(null);
                }
            });

            ExitAppCommand = new DelegateCommand(() =>
            {
                Log.Debug("Closing application");
                configProvider.Save(configProvider.ActualConfig);
                Application.Current.Shutdown();
            });

            this.WhenAnyValue(x => x.WindowState)
                .Subscribe(x => ShowInTaskbar = x != WindowState.Minimized, Log.HandleException)
                .AddTo(Anchors);

            ShowAppCommand = new DelegateCommand(ShowAppCommandExecuted);

            OpenAppDataDirectoryCommand = CommandWrapper.Create(OpenAppDataDirectory);

            ResetOverlayPositionCommand = CommandWrapper.Create(() => ResetOverlayPositionCommandExecuted());
            
            RunAtLoginToggleCommand = CommandWrapper.Create<bool>(RunAtLoginCommandExecuted);

            var executingAssemblyName = Assembly.GetExecutingAssembly().GetName();
            Title = $"{(AppArguments.Instance.IsDebugMode ? "[D]" : "")} {executingAssemblyName.Name} v{executingAssemblyName.Version}";

            // config processing
            Observable.Merge(
                    this.ObservableForProperty(x => x.MicrophoneLine, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsPushToTalkMode, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.AudioNotification, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.HotkeyAlt, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Hotkey, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .Subscribe(() =>
                {
                    var config = configProvider.ActualConfig.CloneJson();
                    config.IsPushToTalkMode = IsPushToTalkMode;
                    config.MicrophoneHotkey = (Hotkey ?? new HotkeyGesture()).ToString();
                    config.MicrophoneHotkeyAlt = (HotkeyAlt ?? new HotkeyGesture()).ToString();
                    config.MicrophoneLineId = MicrophoneLine;
                    config.Notification = AudioNotification;
                    configProvider.Save(config);
                }, Log.HandleException)
                .AddTo(Anchors);
        }

        private async Task RunAtLoginCommandExecuted(bool runAtLogin)
        {
            if (runAtLogin)
            {
                if (!startupManager.Register())
                {
                    Log.Warn("Failed to add application to Auto-start");

                    MessageBox.Show("Failed to change startup parameters");
                }
                else
                {
                    Log.Info($"Application successfully added to Auto-start");
                }
            }
            else
            {
                if (!startupManager.Unregister())
                {
                    Log.Warn("Failed to remove application from Auto-start");
                    MessageBox.Show("Failed to unregister application startup");
                }
                else
                {
                    Log.Info("Application successfully removed from Auto-start");
                }
            }
        }

        private void ResetOverlayPositionCommandExecuted()
        {
            Log.Debug($"Resetting overlay position, current size: {new Rect(Overlay.Left, Overlay.Top, Overlay.Width, Overlay.Height)}");
            Overlay.ResetToDefault();
        }

        private void ShowAppCommandExecuted()
        {
            Log.Debug($"ShowApp command activated, windowState: {WindowState}");
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                Log.Warn($"Main window is not assigned yet");
                return;
            }
            
            Log.Debug($"Activating main window, title: '{mainWindow.Title}' {new Point(mainWindow.Left, mainWindow.Top)}, isActive: {mainWindow.IsActive}, state: {mainWindow.WindowState}, topmost: {mainWindow.Topmost}, style:{mainWindow.WindowStyle}");
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Activate();

            var initialTopmost = mainWindow.Topmost;
            mainWindow.Topmost = !initialTopmost;
            mainWindow.Topmost = initialTopmost;
            
            var mainWindowHandle = new WindowInteropHelper(mainWindow).Handle;
            if (mainWindowHandle != IntPtr.Zero && UnsafeNative.GetForegroundWindow() != mainWindowHandle)
            {
                Log.Debug($"Setting foreground window, hWnd: 0x{mainWindowHandle.ToInt64():x8}, windowState: {WindowState}");
                UnsafeNative.SetForegroundWindow(mainWindowHandle);
            }
        }

        public ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }

        public ICommand ToggleOverlayLockCommand { get; }
        
        public ICommand ResetOverlayPositionCommand { get; }

        public ICommand ExitAppCommand { get; }

        public ICommand ShowAppCommand { get; }

        public CommandWrapper OpenAppDataDirectoryCommand { get; }
        
        public CommandWrapper RunAtLoginToggleCommand { get; }

        public IMicSwitchOverlayViewModel Overlay { get; }

        public IAudioNotificationSelectorViewModel AudioSelectorWhenUnmuted { get; }

        public IAudioNotificationSelectorViewModel AudioSelectorWhenMuted { get; }

        public bool IsActive => mainWindowTracker.IsActive;

        public bool RunAtLogin => startupManager.IsRegistered;

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

        public HotkeyGesture HotkeyAlt
        {
            get => hotkeyAlt;
            set => this.RaiseAndSetIfChanged(ref hotkeyAlt, value);
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

        public TwoStateNotification AudioNotification
        {
            get => new TwoStateNotification
            {
                On = AudioSelectorWhenMuted.SelectedValue,
                Off = AudioSelectorWhenUnmuted.SelectedValue
            };
            set
            {
                AudioSelectorWhenMuted.SelectedValue = value.On;
                AudioSelectorWhenUnmuted.SelectedValue = value.Off;
            }
        }

        public ApplicationUpdaterViewModel ApplicationUpdater { get; }

        private async Task OpenAppDataDirectory()
        {
            await Task.Run(() => Process.Start(ExplorerExecutablePath, AppArguments.Instance.AppDataDirectory));
        }

        private bool IsConfiguredHotkey(HotkeyGesture pressed)
        {
            if (pressed == null)
            {
                return false;
            }

            if (pressed.Key == Key.None && pressed.MouseButton == null)
            {
                return false;
            }

            var pressedHotkey = pressed.ToString();

            return pressedHotkey.Equals(configProvider.ActualConfig.MicrophoneHotkey) || 
                   pressedHotkey.Equals(configProvider.ActualConfig.MicrophoneHotkeyAlt);
        }

        private IObservable<(bool KeyDown, HotkeyGesture Key)> BuildHotkeySubscription(
            IKeyboardEventsSource eventSource)
        {
            var hotkeyDown =
                Observable.Merge(
                        eventSource.WhenMouseDown.Select(x => new HotkeyGesture(x.Button)),
                        eventSource.WhenKeyDown.Select(x => new HotkeyGesture(x.KeyCode.ToInputKey(), x.Modifiers.ToModifiers())))
                    .Where(IsConfiguredHotkey)
                    .Select(x => (KeyDown: true, Key: x));
            var hotkeyUp =
                Observable.Merge(
                        eventSource.WhenMouseUp.Select(x => new HotkeyGesture(x.Button)),
                        eventSource.WhenKeyUp.Select(x => new HotkeyGesture(x.KeyCode.ToInputKey(), x.Modifiers.ToModifiers())))
                    .Where(IsConfiguredHotkey)
                    .Select(x => (KeyDown: false, Key: x));

            return Observable.Merge(hotkeyDown, hotkeyUp);
        }
    }
}