using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DynamicData;
using DynamicData.Binding;
using JetBrains.Annotations;
using log4net;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Input;
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
using ReactiveUI;
using Unity;
using Application = System.Windows.Application;

namespace MicSwitch.MainWindow.ViewModels
{
    internal class MainWindowViewModel : DisposableReactiveObject, IMainWindowViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly string ExplorerExecutablePath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe");
        private static readonly Process CurrentProcess = Process.GetCurrentProcess();
        
        private readonly IWindowTracker mainWindowTracker;
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IAudioNotificationsManager notificationsManager;
        private readonly IWindowViewController viewController;

        private readonly IStartupManager startupManager;
        private readonly IAppArguments appArguments;
        private readonly IMicrophoneControllerEx microphoneController;
        private readonly ObservableAsPropertyHelper<TwoStateNotification> audioNotificationSource;

        private HotkeyGesture hotkey;
        private HotkeyGesture hotkeyAlt;
        private bool suppressHotkey;
        private MicrophoneLineData microphoneLine;
        private bool showInTaskbar;
        private Visibility trayIconVisibility = Visibility.Visible;
        private WindowState windowState;
        private bool startMinimized;
        private Visibility visibility;
        private bool minimizeOnClose;
        private bool microphoneVolumeControlEnabled;
        private MuteMode muteMode;
        private string lastOpenedDirectory;

        public MainWindowViewModel(
            IAppArguments appArguments,
            IFactory<IStartupManager, StartupManagerArgs> startupManagerFactory,
            IMicrophoneControllerEx microphoneController,
            IMicSwitchOverlayViewModel overlay,
            IOverlayWindowController overlayWindowController,
            IAudioNotificationsManager audioNotificationsManager,
            IFactory<IAudioNotificationSelectorViewModel> audioSelectorFactory,
            IApplicationUpdaterViewModel appUpdater,
            [Dependency(WellKnownWindows.MainWindow)] IWindowTracker mainWindowTracker,
            IConfigProvider<MicSwitchConfig> configProvider,
            IComplexHotkeyTracker hotkeyTracker,
            IMicrophoneProvider microphoneProvider,
            IImageProvider imageProvider,
            IAudioNotificationsManager notificationsManager,
            IWindowViewController viewController,
            IHotkeyConverter hotkeyConverter,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            Title = $"{(appArguments.IsDebugMode ? "[D]" : "")} {appArguments.AppName} v{appArguments.Version}";

            this.appArguments = appArguments;
            this.mainWindowTracker = mainWindowTracker;
            this.configProvider = configProvider;
            this.notificationsManager = notificationsManager;
            this.viewController = viewController;
            this.microphoneController = microphoneController.AddTo(Anchors);
            ApplicationUpdater = appUpdater.AddTo(Anchors);
            ImageProvider = imageProvider.AddTo(Anchors);
            AudioSelectorWhenMuted = audioSelectorFactory.Create().AddTo(Anchors);
            AudioSelectorWhenUnmuted = audioSelectorFactory.Create().AddTo(Anchors);
            WindowState = WindowState.Minimized;
            Overlay = overlay;
            
            var startupManagerArgs = new StartupManagerArgs
            {
                UniqueAppName = $"{appArguments.AppName}{(appArguments.IsDebugMode ? "-debug" : string.Empty)}",
                ExecutablePath = appUpdater.GetLatestExecutable().FullName,
                CommandLineArgs = appArguments.StartupArgs,
                AutostartFlag = appArguments.AutostartFlag
            };
            this.startupManager = startupManagerFactory.Create(startupManagerArgs);

            this.RaiseWhenSourceValue(x => x.IsActive, mainWindowTracker, x => x.IsActive, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.RunAtLogin, startupManager, x => x.IsRegistered, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneMuted, microphoneController, x => x.Mute, uiScheduler).AddTo(Anchors);

            microphoneProvider.Microphones
                .ToObservableChangeSet()
                .ObserveOn(uiScheduler)
                .Bind(out var microphones)
                .SubscribeToErrors(Log.HandleUiException)
                .AddTo(Anchors);
            Microphones = microphones;

            this.ObservableForProperty(x => x.MicrophoneMuted, skipInitial: true)
                .DistinctUntilChanged()
                .Where(x => !MicrophoneLine.IsEmpty)
                .Skip(1) // skip initial setup
                .SubscribeSafe(x =>
                {
                    var notificationToPlay = x.Value ? AudioNotification.On : AudioNotification.Off;
                    Log.Debug($"Playing notification {notificationToPlay} (cfg: {AudioNotification.DumpToTextRaw()})");
                    audioNotificationsManager.PlayNotification(notificationToPlay);
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.MuteMode)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(newMuteMode =>
                {
                    switch (newMuteMode)
                    {
                        case MuteMode.PushToTalk:
                            Log.Debug($"{newMuteMode} mute mode is enabled, un-muting microphone");
                            MuteMicrophoneCommand.Execute(true);
                            break;
                        case MuteMode.PushToMute:
                            MuteMicrophoneCommand.Execute(false);
                            Log.Debug($"{newMuteMode} mute mode is enabled, muting microphone");
                            break;
                        default:
                           Log.Debug($"{newMuteMode} enabled, mic action is not needed");
                           break;
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);  

            this.WhenAnyValue(x => x.MicrophoneLine)
                .DistinctUntilChanged()
                .SubscribeSafe(x => microphoneController.LineId = x, Log.HandleUiException)
                .AddTo(Anchors);

            audioNotificationSource = Observable.Merge(
                    AudioSelectorWhenMuted.ObservableForProperty(x => x.SelectedValue, skipInitial: true),
                    AudioSelectorWhenUnmuted.ObservableForProperty(x => x.SelectedValue, skipInitial: true))
                .Select(x => new TwoStateNotification
                {
                    On = AudioSelectorWhenUnmuted.SelectedValue,
                    Off = AudioSelectorWhenMuted.SelectedValue
                })
                .ToPropertyHelper(this, x => x.AudioNotification)
                .AddTo(Anchors);

           hotkeyTracker
                .WhenAnyValue(x => x.IsActive)
                .Skip(1)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(async isActive =>
                {
                    Log.Debug($"Handling hotkey press (isActive: {isActive}), mute mode: {muteMode}");
                    switch (muteMode)
                    {
                        case MuteMode.PushToTalk:
                            await MuteMicrophoneCommandExecuted(!isActive);
                            break;
                        case MuteMode.PushToMute:
                            await MuteMicrophoneCommandExecuted(isActive);
                            break;
                        case MuteMode.ToggleMute:
                            await MuteMicrophoneCommandExecuted(!MicrophoneMuted);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(muteMode), muteMode, @"Unsupported mute mode");
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.WindowState)
                .SubscribeSafe(x => ShowInTaskbar = x != WindowState.Minimized, Log.HandleUiException)
                .AddTo(Anchors);
            
            viewController
                .WhenClosing
                .SubscribeSafe(x => HandleWindowClosing(viewController, x), Log.HandleUiException)
                .AddTo(Anchors);

            ToggleOverlayLockCommand = CommandWrapper.Create(ToggleOverlayCommandExecuted);
            ExitAppCommand = CommandWrapper.Create(ExitAppCommandExecuted);
            ShowAppCommand = CommandWrapper.Create(ShowAppCommandExecuted);
            OpenAppDataDirectoryCommand = CommandWrapper.Create(OpenAppDataDirectory);
            ResetOverlayPositionCommand = CommandWrapper.Create(ResetOverlayPositionCommandExecuted);
            RunAtLoginToggleCommand = CommandWrapper.Create<bool>(RunAtLoginCommandExecuted);
            MuteMicrophoneCommand = CommandWrapper.Create<bool>(MuteMicrophoneCommandExecuted);
            SelectMicrophoneIconCommand = CommandWrapper.Create(SelectMicrophoneIconCommandExecuted);
            SelectMutedMicrophoneIconCommand = CommandWrapper.Create(SelectMutedMicrophoneIconCommandExecuted);
            ResetMicrophoneIconsCommand = CommandWrapper.Create(ResetMicrophoneIconsCommandExecuted);
            AddSoundCommand = CommandWrapper.Create(AddSoundCommandExecuted);

            configProvider.ListenTo(x => x.Notification)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(cfg =>
                {
                    Log.Debug($"Applying new notification configuration: {cfg.DumpToTextRaw()} (current: {AudioNotification.DumpToTextRaw()})");
                    AudioSelectorWhenMuted.SelectedValue = cfg.Off;
                    AudioSelectorWhenUnmuted.SelectedValue = cfg.On;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.MuteMode)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"Mute mode loaded from config: {x}");
                    MuteMode = x;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.SuppressHotkey)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x => SuppressHotkey = x, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.MinimizeOnClose)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x => MinimizeOnClose = x, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.VolumeControlEnabled)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x => MicrophoneVolumeControlEnabled = x, Log.HandleException)
                .AddTo(Anchors);
            
            Observable.Merge(
                    configProvider.ListenTo(x => x.MicrophoneLineId).ToUnit(),
                    Microphones.ToObservableChangeSet().ToUnit())
                .Select(_ => configProvider.ActualConfig.MicrophoneLineId)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(configLineId =>
                {
                    Log.Debug($"Microphone line configuration changed, lineId: {configLineId}, known lines: {Microphones.DumpToTextRaw()}");

                    var micLine = Microphones.FirstOrDefault(line => line.Equals(configLineId));
                    if (micLine.IsEmpty)
                    {
                        Log.Debug($"Selecting first one of available microphone lines, known lines: {Microphones.DumpToTextRaw()}");
                        micLine = Microphones.FirstOrDefault();
                    }
                    MicrophoneLine = micLine;
                    MuteMicrophoneCommand.ResetError();
                }, Log.HandleUiException)
                .AddTo(Anchors);

            Observable.Merge(configProvider.ListenTo(x => x.MicrophoneHotkey), configProvider.ListenTo(x => x.MicrophoneHotkeyAlt))
                .Select(x =>
                {
                    try
                    {
                        return new
                        {
                            Hotkey = hotkeyConverter.ConvertFromString(configProvider.ActualConfig.MicrophoneHotkey ?? string.Empty),
                            HotkeyAlt = hotkeyConverter.ConvertFromString(configProvider.ActualConfig.MicrophoneHotkeyAlt ?? string.Empty),
                        };
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to parse config hotkeys: {new { configProvider.ActualConfig.MicrophoneHotkey, configProvider.ActualConfig.MicrophoneHotkeyAlt }}", e);
                        return new
                        {
                            Hotkey = HotkeyGesture.Empty,
                            HotkeyAlt = HotkeyGesture.Empty
                        };
                    }
                })
                .ObserveOn(uiScheduler)
                .SubscribeSafe(cfg =>
                {
                    Log.Debug($"Setting new hotkeys configuration: {cfg.DumpToTextRaw()} (current: {hotkey}, alt: {hotkeyAlt})");
                    Hotkey = cfg.Hotkey;
                    HotkeyAlt = cfg.HotkeyAlt;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            viewController
                .WhenLoaded
                .Take(1)
                .Select(_ => configProvider.ListenTo(y => y.StartMinimized))
                .Switch()
                .Take(1)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(
                    x =>
                    {
                        if (x)
                        {
                            Log.Debug($"StartMinimized option is active - minimizing window, current state: {WindowState}");
                            StartMinimized = true;
                            viewController.Hide();
                        }
                        else
                        {
                            Log.Debug($"StartMinimized option is not active - showing window as Normal, current state: {WindowState}");
                            StartMinimized = false;
                            viewController.Show();
                        }
                    }, Log.HandleUiException)
                .AddTo(Anchors);

            Observable.Merge(
                    this.ObservableForProperty(x => x.MicrophoneLine, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.MuteMode, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.AudioNotification, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.HotkeyAlt, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Hotkey, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.SuppressHotkey, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.MinimizeOnClose, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.MicrophoneVolumeControlEnabled, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.StartMinimized, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() =>
                {
                    var config = configProvider.ActualConfig.CloneJson();
                    config.MuteMode = muteMode;
                    config.MicrophoneHotkey = hotkeyConverter.ConvertToString(Hotkey);
                    config.MicrophoneHotkeyAlt = hotkeyConverter.ConvertToString(HotkeyAlt);
                    config.MicrophoneLineId = MicrophoneLine;
                    config.Notification = AudioNotification;
                    config.SuppressHotkey = SuppressHotkey;
                    config.StartMinimized = StartMinimized;
                    config.MinimizeOnClose = MinimizeOnClose;
                    configProvider.Save(config);
                }, Log.HandleUiException)
                .AddTo(Anchors);

            viewController.WhenLoaded
                .SubscribeSafe(() =>
                {
                    Log.Debug($"Main window loaded - loading overlay, current process({CurrentProcess.ProcessName} 0x{CurrentProcess.Id:x8}) main window: {CurrentProcess.MainWindowHandle} ({CurrentProcess.MainWindowTitle})");
                    overlayWindowController.RegisterChild(overlay).AddTo(Anchors);
                    Log.Debug("Overlay loaded successfully");
                }, Log.HandleUiException)
                .AddTo(Anchors);
        }

        private void ToggleOverlayCommandExecuted()
        {
            if (Overlay.IsLocked && Overlay.UnlockWindowCommand.CanExecute(null))
            {
                Overlay.UnlockWindowCommand.Execute(null);
            }
            else if (!Overlay.IsLocked && Overlay.LockWindowCommand.CanExecute(null))
            {
                Overlay.LockWindowCommand.Execute(null);
            }
        }

        private void ExitAppCommandExecuted()
        {
            Log.Debug("Closing application");
            configProvider.Save(configProvider.ActualConfig);
            Application.Current.Shutdown();
        }

        private void ShowAppCommandExecuted()
        {
            if (Visibility != Visibility.Visible)
            {
                Log.Debug($"Showing application, currents state: {Visibility}");
                viewController.Show();
            }
            else
            {
                Log.Debug($"Hiding application, currents state: {Visibility}");
                viewController.Hide();
            }
        }

        private void HandleWindowClosing(IWindowViewController viewController, CancelEventArgs args)
        {
            Log.Info($"Main window is closing(cancel: {args.Cancel}), {nameof(Visibility)}: {Visibility}, {nameof(MicSwitchConfig.MinimizeOnClose)}: {configProvider.ActualConfig.MinimizeOnClose}");
            if (MinimizeOnClose)
            {
                Log.Info("Cancelling main window closure (will be ignored during app shutdown)");
                args.Cancel = true;
                viewController.Hide();
            }
        }

        private async Task SelectMicrophoneIconCommandExecuted()
        {
            var icon = await SelectIcon();
            if (icon == null)
            {
                return;
            }

            var config = configProvider.ActualConfig.CloneJson();
            config.MicrophoneIcon = icon;
            configProvider.Save(config);
        }
        
        private async Task SelectMutedMicrophoneIconCommandExecuted()
        {
            var icon = await SelectIcon();
            if (icon == null)
            {
                return;
            }

            var config = configProvider.ActualConfig.CloneJson();
            config.MutedMicrophoneIcon = icon;
            configProvider.Save(config);
        }

        private async Task ResetMicrophoneIconsCommandExecuted()
        {
            var config = configProvider.ActualConfig.CloneJson();
            config.MicrophoneIcon = null;
            config.MutedMicrophoneIcon = null;
            configProvider.Save(config);
        }

        private static async Task<byte[]> SelectIcon()
        {
            Log.Info($"Showing OpenFileDialog to user");

            var initialDirectory = string.Empty;
            var op = new OpenFileDialog
            {
                Title = "Select an icon", 
                InitialDirectory = !string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory) 
                    ? initialDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures),
                CheckPathExists = true,
                Multiselect = false,
                Filter = "All supported graphics|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
            };

            if (op.ShowDialog() != true)
            {
                Log.Info("User cancelled OpenFileDialog");
                return null;
            }

            Log.Debug($"Opening image {op.FileName}");
            return await File.ReadAllBytesAsync(op.FileName);
        }

        private async Task MuteMicrophoneCommandExecuted(bool mute)
        {
            Log.Debug($"{(mute ? "Muting" : "Un-muting")} microphone {microphoneController.LineId}");
            microphoneController.Mute = mute;
        }

        private async Task RunAtLoginCommandExecuted(bool runAtLogin)
        {
            if (runAtLogin)
            {
                if (!startupManager.Register())
                {
                    Log.Warn("Failed to add application to Auto-start");
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
                }
                else
                {
                    Log.Info("Application successfully removed from Auto-start");
                }
            }
        }

        private void ResetOverlayPositionCommandExecuted()
        {
            Log.Debug($"Resetting overlay position, current size: {Overlay.NativeBounds}");
            Overlay.ResetToDefault();
        }

        public bool IsElevated => appArguments.IsElevated;

        public ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }

        public ICommand ToggleOverlayLockCommand { get; }
        
        public ICommand ResetOverlayPositionCommand { get; }

        public ICommand ExitAppCommand { get; }

        public ICommand ShowAppCommand { get; }
        
        public ICommand SelectMutedMicrophoneIconCommand { get; }
        
        public ICommand SelectMicrophoneIconCommand { get; }
        
        public ICommand ResetMicrophoneIconsCommand { get; }

        public CommandWrapper OpenAppDataDirectoryCommand { get; }
        
        public CommandWrapper RunAtLoginToggleCommand { get; }

        public CommandWrapper MuteMicrophoneCommand { get; }
        
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

        public Visibility Visibility
        {
            get => visibility;
            set => this.RaiseAndSetIfChanged(ref visibility, value);
        }

        public bool StartMinimized
        {
            get => startMinimized;
            set => this.RaiseAndSetIfChanged(ref startMinimized, value);
        }

        public bool MinimizeOnClose
        {
            get => minimizeOnClose;
            set => RaiseAndSetIfChanged(ref minimizeOnClose, value);
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

        public MuteMode MuteMode
        {
            get => muteMode;
            set => RaiseAndSetIfChanged(ref muteMode, value);
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

        public bool MicrophoneVolumeControlEnabled
        {
            get => microphoneVolumeControlEnabled;
            set => RaiseAndSetIfChanged(ref microphoneVolumeControlEnabled, value);
        }

        public bool MicrophoneMuted
        {
            get => microphoneController.Mute ?? false;
        }
        
        public IImageProvider ImageProvider { get; }

        public bool SuppressHotkey
        {
            get => suppressHotkey;
            set => this.RaiseAndSetIfChanged(ref suppressHotkey, value);
        }
        
        public CommandWrapper AddSoundCommand { get; }
        
        public string LastOpenedDirectory
        {
            get => lastOpenedDirectory;
            private set => RaiseAndSetIfChanged(ref lastOpenedDirectory, value);
        }

        public TwoStateNotification AudioNotification => audioNotificationSource.Value;

        public IApplicationUpdaterViewModel ApplicationUpdater { get; }

        public bool IsDebugMode => appArguments.IsDebugMode;

        private async Task OpenAppDataDirectory()
        {
            await Task.Run(() => Process.Start(ExplorerExecutablePath, appArguments.AppDataDirectory));
        }
        
        private void AddSoundCommandExecuted()
        {
            Log.Info($"Showing OpenFileDialog to user");

            var op = new OpenFileDialog
            {
                Title = "Select an image", 
                InitialDirectory = !string.IsNullOrEmpty(LastOpenedDirectory) && Directory.Exists(LastOpenedDirectory) 
                    ? LastOpenedDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.CommonMusic),
                CheckPathExists = true,
                Multiselect = false,
                Filter = "All supported sound files|*.wav;*.mp3|All files|*.*"
            };

            if (op.ShowDialog() != true)
            {
                return;
            }

            Log.Debug($"Adding notification {op.FileName}");
            LastOpenedDirectory = Path.GetDirectoryName(op.FileName);
            var notification = notificationsManager.AddFromFile(new FileInfo(op.FileName));
            Log.Debug($"Added notification {notification}, list of notifications: {notificationsManager.Notifications.JoinStrings(", ")}");
        }
    }
}