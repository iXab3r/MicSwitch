using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using log4net;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using PoeShared.Audio.Services;
using PoeShared.Squirrel.Updater;
using Application = System.Windows.Application;
using Size = System.Windows.Size;

#pragma warning disable 1998

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
        private readonly IConfigProvider<MicSwitchOverlayConfig> overlayConfigProvider;
        private readonly IAudioNotificationsManager notificationsManager;
        private readonly IWindowViewController viewController;
        private readonly IRandomNumberGenerator rng;

        private readonly IStartupManager startupManager;
        private readonly IAppArguments appArguments;
        private readonly IApplicationAccessor applicationAccessor;
        private readonly ObservableAsPropertyHelper<TwoStateNotification> audioNotificationSource;

        private bool showInTaskbar;
        private Visibility trayIconVisibility = Visibility.Visible;
        private WindowState windowState;
        private bool startMinimized;
        private Visibility visibility;
        private bool minimizeOnClose;
        private string lastOpenedDirectory;
        private float audioNotificationVolume;
        private double height;
        private double left;
        private double top;
        private double width;
        
        public MainWindowViewModel(
            IAppArguments appArguments,
            IApplicationAccessor applicationAccessor,
            IFactory<IStartupManager, StartupManagerArgs> startupManagerFactory,
            IMicSwitchOverlayViewModel overlay,
            IMicrophoneControllerViewModel microphoneControllerViewModel,
            IOverlayWindowController overlayWindowController,
            IWaveOutDeviceSelectorViewModel waveOutDeviceSelector,
            IAudioNotificationsManager audioNotificationsManager,
            IFactory<IAudioNotificationSelectorViewModel> audioSelectorFactory,
            IApplicationUpdaterViewModel appUpdater,
            [Dependency(WellKnownWindows.MainWindow)] IWindowTracker mainWindowTracker,
            IConfigProvider<MicSwitchConfig> configProvider,
            IConfigProvider<MicSwitchOverlayConfig> overlayConfigProvider,
            IImageProvider imageProvider,
            IErrorMonitorViewModel errorMonitor,
            IAudioNotificationsManager notificationsManager,
            IWindowViewController viewController,
            IRandomNumberGenerator rng,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            Title = $"{(appArguments.IsDebugMode ? "[D]" : "")} {appArguments.AppName} v{appArguments.Version}";

            this.appArguments = appArguments;
            this.applicationAccessor = applicationAccessor;
            this.MicrophoneController = microphoneControllerViewModel.AddTo(Anchors);
            this.mainWindowTracker = mainWindowTracker;
            this.configProvider = configProvider;
            this.overlayConfigProvider = overlayConfigProvider;
            this.notificationsManager = notificationsManager;
            this.viewController = viewController;
            this.rng = rng;
            ApplicationUpdater = appUpdater.AddTo(Anchors);
            WaveOutDeviceSelector = waveOutDeviceSelector;
            ImageProvider = imageProvider;
            ErrorMonitor = errorMonitor;
            AudioSelectorWhenMuted = audioSelectorFactory.Create().AddTo(Anchors);
            AudioSelectorWhenUnmuted = audioSelectorFactory.Create().AddTo(Anchors);
            WindowState = WindowState.Minimized;
            Overlay = overlay.AddTo(Anchors);
            
            try
            {
                var startupManagerArgs = new StartupManagerArgs
                {
                    UniqueAppName = $"{appArguments.AppName}{(appArguments.IsDebugMode ? "-debug" : string.Empty)}",
                    ExecutablePath = appUpdater.LauncherExecutable.FullName,
                    CommandLineArgs = appArguments.StartupArgs,
                    AutostartFlag = appArguments.AutostartFlag
                };
                this.startupManager = startupManagerFactory.Create(startupManagerArgs);
                RunAtLoginToggleCommand = CommandWrapper.Create<bool>(RunAtLoginCommandExecuted,  Observable.Return(startupManager?.IsReady ?? false));
            }
            catch (Exception e)
            {
                Log.Warn("Failed to initialize startup manager", e);
            }

            this.RaiseWhenSourceValue(x => x.IsActive, mainWindowTracker, x => x.IsActive, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.RunAtLogin, startupManager, x => x.IsRegistered, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.ShowOverlaySettings, Overlay, x => x.OverlayVisibilityMode).AddTo(Anchors);

            audioNotificationSource = Observable.Merge(
                    AudioSelectorWhenMuted.ObservableForProperty(x => x.SelectedValue, skipInitial: true),
                    AudioSelectorWhenUnmuted.ObservableForProperty(x => x.SelectedValue, skipInitial: true))
                .Select(x => new TwoStateNotification
                {
                    On = AudioSelectorWhenUnmuted.SelectedValue,
                    Off = AudioSelectorWhenMuted.SelectedValue
                })
                .ToProperty(this, x => x.AudioNotification)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.AudioNotificationVolume)
                .Subscribe(x =>
                {
                    AudioSelectorWhenUnmuted.Volume = AudioSelectorWhenMuted.Volume = x;
                })
                .AddTo(Anchors);
            
            MicrophoneController.ObservableForProperty(x => x.MicrophoneMuted, skipInitial: true)
                .DistinctUntilChanged()
                .Where(x => !MicrophoneController.MMDeviceLine.IsEmpty)
                .Select(isMuted => (isMuted.Value ? AudioNotification.Off : AudioNotification.On) ?? default(AudioNotificationType).ToString())
                .Where(notificationToPlay => !string.IsNullOrEmpty(notificationToPlay))
                .Select(notificationToPlay => Observable.FromAsync(async token =>
                {
                    Log.Debug($"Playing notification {notificationToPlay}, volume: {audioNotificationVolume}");
                    try
                    {
                        await audioNotificationsManager.PlayNotification(notificationToPlay, audioNotificationVolume, waveOutDeviceSelector.SelectedItem, token);
                        Log.Debug($"Played notification {notificationToPlay}");
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Failed to play notification {notificationToPlay}", ex);
                    }
                }))
                .Switch()
                .SubscribeToErrors(Log.HandleUiException)
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
            RunAtLoginToggleCommand = CommandWrapper.Create<bool>(RunAtLoginCommandExecuted, startupManager.WhenAnyValue(x => x.IsReady));
            SelectMicrophoneIconCommand = CommandWrapper.Create(SelectMicrophoneIconCommandExecuted);
            SelectMutedMicrophoneIconCommand = CommandWrapper.Create(SelectMutedMicrophoneIconCommandExecuted);
            ResetMicrophoneIconsCommand = CommandWrapper.Create(ResetMicrophoneIconsCommandExecuted);
            AddSoundCommand = CommandWrapper.Create(AddSoundCommandExecuted);
            PlaySoundCommand = CommandWrapper.Create(PlaySoundCommandExecuted);

            Observable.Merge(configProvider.ListenTo(x => x.Notifications).ToUnit(), configProvider.ListenTo(x => x.NotificationVolume).ToUnit())
                .Select(_ => new { configProvider.ActualConfig.Notifications, configProvider.ActualConfig.NotificationVolume })
                .ObserveOn(uiScheduler)
                .SubscribeSafe(cfg =>
                {
                    Log.Debug($"Applying new notification configuration: {cfg.DumpToTextRaw()} (current: {AudioNotification.DumpToTextRaw()}, volume: {AudioNotificationVolume})");
                    AudioSelectorWhenMuted.SelectedValue = cfg.Notifications.Off;
                    AudioSelectorWhenUnmuted.SelectedValue = cfg.Notifications.On;
                    AudioNotificationVolume = cfg.NotificationVolume;
                }, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.MinimizeOnClose)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x => MinimizeOnClose = x, Log.HandleException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.OutputDeviceId)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x => WaveOutDeviceSelector.SelectById(x), Log.HandleException)
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
                    microphoneControllerViewModel.ObservableForProperty(x => x.MuteMode, skipInitial: true).ToUnit(),
                    waveOutDeviceSelector.ObservableForProperty(x => x.SelectedItem, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.AudioNotification, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.MinimizeOnClose, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Width, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Height, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Top, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Left, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.AudioNotificationVolume, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.StartMinimized, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() =>
                {
                    var config = configProvider.ActualConfig.CloneJson();
                    config.Notifications = AudioNotification;
                    config.NotificationVolume = AudioNotificationVolume;
                    config.StartMinimized = StartMinimized;
                    config.MinimizeOnClose = MinimizeOnClose;
                    config.OutputDeviceId = waveOutDeviceSelector.SelectedItem?.Id;
                    config.MainWindowBounds = new Rect(Left, Top, Width, Height);
                    configProvider.Save(config);
                }, Log.HandleUiException)
                .AddTo(Anchors);

            viewController.WhenLoaded
                .SubscribeSafe(() =>
                {
                    Log.Debug($"Main window loaded - loading overlay, current process({CurrentProcess.ProcessName} 0x{CurrentProcess.Id:x8}) main window: {CurrentProcess.MainWindowHandle} ({CurrentProcess.MainWindowTitle})");
                    overlayWindowController.RegisterChild(Overlay).AddTo(Anchors);
                    Log.Debug("Overlay loaded successfully");
                }, Log.HandleUiException)
                .AddTo(Anchors);

            configProvider.ListenTo(x => x.MainWindowBounds)
                .WithPrevious()
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"Main window config bounds updated: {x}");

                    Rect bounds;
                    if (x.Current == null)
                    {
                        var monitorBounds = UnsafeNative.GetMonitorBounds(Rectangle.Empty).ScaleToWpf();
                        var monitorCenter = monitorBounds.Center();
                        bounds = new Rect(
                            monitorCenter.X - DefaultSize.Width / 2f,
                            monitorCenter.Y - DefaultSize.Height / 2f,
                            DefaultSize.Width,
                            DefaultSize.Height);
                    }
                    else
                    {
                        bounds = x.Current.Value;
                    }

                    Left = bounds.Left;
                    Top = bounds.Top;
                    Width = bounds.Width;
                    Height = bounds.Height;
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            var theme = Theme.Create(
                Theme.Light, 
                primary: SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.BlueGrey], 
                accent: SwatchHelper.Lookup[(MaterialDesignColor) SecondaryColor.LightBlue]);
            var paletteHelper = new PaletteHelper();
            paletteHelper.SetTheme(theme);
        }

        public bool IsElevated => appArguments.IsElevated;

        public ICommand ToggleOverlayLockCommand { get; }

        public ICommand ResetOverlayPositionCommand { get; }

        public ICommand ExitAppCommand { get; }

        public ICommand ShowAppCommand { get; }

        public ICommand SelectMutedMicrophoneIconCommand { get; }

        public ICommand SelectMicrophoneIconCommand { get; }

        public ICommand ResetMicrophoneIconsCommand { get; }

        public CommandWrapper OpenAppDataDirectoryCommand { get; }

        public CommandWrapper RunAtLoginToggleCommand { get; }

        public IMicSwitchOverlayViewModel Overlay { get; }

        public IAudioNotificationSelectorViewModel AudioSelectorWhenUnmuted { get; }

        public IAudioNotificationSelectorViewModel AudioSelectorWhenMuted { get; }

        public IMicrophoneControllerViewModel MicrophoneController { get; }

        public bool IsActive => mainWindowTracker.IsActive;

        public bool RunAtLogin => startupManager?.IsRegistered ?? false;

        public Size MinSize { get; } = new Size(600, 430);

        public Size MaxSize { get; } = new Size(900, 980);

        public Size DefaultSize { get; } = new Size(600, 680);

        private async Task PlaySoundCommandExecuted()
        {
            var notifications = new[] {AudioSelectorWhenMuted, AudioSelectorWhenUnmuted}
                .Where(x => !string.IsNullOrEmpty(x.SelectedValue))
                .ToArray();

            var notificationToPlay = notifications.Any() ? notifications.PickRandom().SelectedValue : AudioNotificationType.Bell.ToString();
            await notificationsManager.PlayNotification(notificationToPlay, AudioNotificationVolume);
        }

        public WindowState WindowState
        {
            get => windowState;
            set => this.RaiseAndSetIfChanged(ref windowState, value);
        }

        public bool ShowOverlaySettings => Overlay.OverlayVisibilityMode != OverlayVisibilityMode.Never;

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

        public string Title { get; }

        public IWaveOutDeviceSelectorViewModel WaveOutDeviceSelector { get; }
        
        public IImageProvider ImageProvider { get; }
        
        public IErrorMonitorViewModel ErrorMonitor { get; }

        public CommandWrapper AddSoundCommand { get; }
        
        public CommandWrapper PlaySoundCommand { get; }
        
        public string LastOpenedDirectory
        {
            get => lastOpenedDirectory;
            private set => RaiseAndSetIfChanged(ref lastOpenedDirectory, value);
        }

        public double Width
        {
            get => width;
            set => RaiseAndSetIfChanged(ref width, value);
        }

        public double Height
        {
            get => height;
            set => RaiseAndSetIfChanged(ref height, value);
        }

        public double Left
        {
            get => left;
            set => RaiseAndSetIfChanged(ref left, value);
        }

        public double Top
        {
            get => top;
            set => RaiseAndSetIfChanged(ref top, value);
        }

        public TwoStateNotification AudioNotification => audioNotificationSource.Value;

        public float AudioNotificationVolume
        {
            get => audioNotificationVolume;
            set => RaiseAndSetIfChanged(ref audioNotificationVolume, value);
        }

        public IApplicationUpdaterViewModel ApplicationUpdater { get; }

        public bool IsDebugMode => appArguments.IsDebugMode;
        
        private async Task ToggleOverlayCommandExecuted()
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

        private async Task ExitAppCommandExecuted()
        {
            Log.Debug("Closing application");
            configProvider.Save(configProvider.ActualConfig);
            Application.Current.Shutdown();
        }

        private async Task ShowAppCommandExecuted()
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

        private void HandleWindowClosing(IViewController viewController, CancelEventArgs args)
        {
            Log.Info($"Main window is closing(cancel: {args.Cancel}), {nameof(Visibility)}: {Visibility}, {nameof(MicSwitchConfig.MinimizeOnClose)}: {configProvider.ActualConfig.MinimizeOnClose}, {nameof(applicationAccessor.IsExiting)}: {applicationAccessor.IsExiting}");
            if (MinimizeOnClose && !applicationAccessor.IsExiting)
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

            var config = overlayConfigProvider.ActualConfig.CloneJson();
            config.MicrophoneIcon = icon;
            overlayConfigProvider.Save(config);
        }
        
        private async Task SelectMutedMicrophoneIconCommandExecuted()
        {
            var icon = await SelectIcon();
            if (icon == null)
            {
                return;
            }

            var config = overlayConfigProvider.ActualConfig.CloneJson();
            config.MutedMicrophoneIcon = icon;
            overlayConfigProvider.Save(config);
        }

        private async Task ResetMicrophoneIconsCommandExecuted()
        {
            var config = overlayConfigProvider.ActualConfig.CloneJson();
            config.MicrophoneIcon = null;
            config.MutedMicrophoneIcon = null;
            overlayConfigProvider.Save(config);
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

        private async Task ResetOverlayPositionCommandExecuted()
        {
            Log.Debug($"Resetting overlay position, current size: {Overlay.NativeBounds}");
            Overlay.ResetToDefault();
        }

        private async Task OpenAppDataDirectory()
        {
            await Task.Run(() => Process.Start(ExplorerExecutablePath, appArguments.AppDataDirectory));
        }

        private async Task AddSoundCommandExecuted()
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
    }
}