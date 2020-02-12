using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using DynamicData.Binding;
using JetBrains.Annotations;
using log4net;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Services;
using PoeShared;
using PoeShared.Audio.Services;
using PoeShared.Audio.ViewModels;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;
using PoeShared.Services;
using PoeShared.Squirrel.Updater;
using PoeShared.UI.Hotkeys;
using Prism.Commands;
using ReactiveUI;
using Unity;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MicSwitch.MainWindow.ViewModels
{
    internal class MainWindowViewModel : DisposableReactiveObject
    {
        private static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private static readonly string ExplorerExecutablePath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe");
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IWindowTracker mainWindowTracker;

        private readonly IStartupManager startupManager;
        private readonly IAppArguments appArguments;
        private readonly IMicrophoneController microphoneController;

        private HotkeyGesture hotkey;
        private HotkeyGesture hotkeyAlt;
        private bool isPushToTalkMode;
        private bool suppressHotkey;
        private MicrophoneLineData microphoneLine;
        private bool showInTaskbar;
        private Visibility trayIconVisibility;
        private WindowState windowState;
        private bool startMinimized;

        public MainWindowViewModel(
            [NotNull] IAppArguments appArguments,
            [NotNull] IFactory<IStartupManager, StartupManagerArgs> startupManagerFactory,
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IMicSwitchOverlayViewModel overlay,
            [NotNull] IAudioNotificationsManager audioNotificationsManager,
            [NotNull] IFactory<IAudioNotificationSelectorViewModel> audioSelectorFactory,
            [NotNull] IApplicationUpdaterViewModel appUpdater,
            [NotNull] [Dependency(WellKnownWindows.MainWindow)] IWindowTracker mainWindowTracker,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] IComplexHotkeyTracker hotkeyTracker,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            var restartArgs = appUpdater.GetRestartApplicationArgs();
            var startupManagerArgs = new StartupManagerArgs
            {
                UniqueAppName = $"{appArguments.AppName}{(appArguments.IsDebugMode ? "-debug" : string.Empty)}",
                ExecutablePath = restartArgs.exePath,
                CommandLineArgs = restartArgs.exeArgs,
                AutostartFlag = appArguments.AutostartFlag
            };
           
            this.startupManager = startupManagerFactory.Create(startupManagerArgs);

            this.appArguments = appArguments;
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
                .ObserveOn(uiScheduler)
                .Subscribe(cfg =>
                {
                    Log.Debug($"Applying new notification configuration: {cfg.DumpToTextRaw()} (current: {AudioNotification.DumpToTextRaw()})");
                    AudioNotification = cfg;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.IsPushToTalkMode)
                .ObserveOn(uiScheduler)
                .Subscribe(x => IsPushToTalkMode = x, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.SuppressHotkey)
                .ObserveOn(uiScheduler)
                .Subscribe(x => SuppressHotkey = x, Log.HandleException)
                .AddTo(Anchors);

            Observable.Merge(configProvider.ListenTo(x => x.MicrophoneHotkey), configProvider.ListenTo(x => x.MicrophoneHotkeyAlt))
                .Select(x => new
                {
                    Hotkey = (HotkeyGesture)new HotkeyConverter().ConvertFrom(configProvider.ActualConfig.MicrophoneHotkey ?? string.Empty), 
                    HotkeyAlt = (HotkeyGesture)new HotkeyConverter().ConvertFrom(configProvider.ActualConfig.MicrophoneHotkeyAlt ?? string.Empty), 
                })
                .ObserveOn(uiScheduler)
                .Subscribe(cfg =>
                {
                    Log.Debug($"Setting new hotkeys configuration: {cfg.DumpToTextRaw()} (current: {hotkey}, alt: {hotkeyAlt})");
                    Hotkey = cfg.Hotkey;
                    HotkeyAlt = cfg.HotkeyAlt;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            Overlay = overlay;

            this.RaiseWhenSourceValue(x => x.RunAtLogin, startupManager, x => x.IsRegistered).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneMuted, microphoneController, x => x.Mute).AddTo(Anchors);

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
                .ObserveOn(uiScheduler)
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

            hotkeyTracker
                .WhenAnyValue(x => x.IsActive)
                .ObserveOn(uiScheduler)
                .Subscribe(isActive =>
                {
                    MicrophoneMuted = isActive;
                }, Log.HandleException)
                .AddTo(Anchors);

            ToggleOverlayLockCommand = CommandWrapper.Create(
                () =>
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

            ExitAppCommand = CommandWrapper.Create(
                () =>
                {
                    Log.Debug("Closing application");
                    configProvider.Save(configProvider.ActualConfig);
                    Application.Current.Shutdown();
                });

            this.WhenAnyValue(x => x.WindowState)
                .Subscribe(x => ShowInTaskbar = x != WindowState.Minimized, Log.HandleException)
                .AddTo(Anchors);

            ShowAppCommand = CommandWrapper.Create(() => ShowAppCommandExecuted());

            OpenAppDataDirectoryCommand = CommandWrapper.Create(OpenAppDataDirectory);

            ResetOverlayPositionCommand = CommandWrapper.Create(() => ResetOverlayPositionCommandExecuted());
            
            RunAtLoginToggleCommand = CommandWrapper.Create<bool>(RunAtLoginCommandExecuted);

            var executingAssemblyName = Assembly.GetExecutingAssembly().GetName();
            Title = $"{(appArguments.IsDebugMode ? "[D]" : "")} {executingAssemblyName.Name} v{executingAssemblyName.Version}";

            configProvider.ListenTo(x => x.StartMinimized)
                .Take(1)
                .Where(x => x)
                .ObserveOn(uiScheduler)
                .Subscribe(
                    x =>
                    {
                        Log.Debug($"StartMinimized option is active - minimizing window, current state: {WindowState}");
                        StartMinimized = true;
                        WindowState = WindowState.Minimized;
                    })
                .AddTo(Anchors);

            // config processing
            Observable.Merge(
                    this.ObservableForProperty(x => x.MicrophoneLine, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsPushToTalkMode, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.AudioNotification, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.HotkeyAlt, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Hotkey, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.SuppressHotkey, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.StartMinimized, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .Subscribe(() =>
                {
                    var config = configProvider.ActualConfig.CloneJson();
                    config.IsPushToTalkMode = IsPushToTalkMode;
                    config.MicrophoneHotkey = (Hotkey ?? new HotkeyGesture()).ToString();
                    config.MicrophoneHotkeyAlt = (HotkeyAlt ?? new HotkeyGesture()).ToString();
                    config.MicrophoneLineId = MicrophoneLine;
                    config.Notification = AudioNotification;
                    config.SuppressHotkey = SuppressHotkey;
                    config.StartMinimized = StartMinimized;
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
        
        public bool IsElevated => appArguments.IsElevated;

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

        public bool StartMinimized
        {
            get => startMinimized;
            set => this.RaiseAndSetIfChanged(ref startMinimized, value);
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

        public bool SuppressHotkey
        {
            get => suppressHotkey;
            set => this.RaiseAndSetIfChanged(ref suppressHotkey, value);
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

        public IApplicationUpdaterViewModel ApplicationUpdater { get; }

        public bool IsDebugMode => appArguments.IsDebugMode;

        private async Task OpenAppDataDirectory()
        {
            await Task.Run(() => Process.Start(ExplorerExecutablePath, appArguments.AppDataDirectory));
        }
    }
}