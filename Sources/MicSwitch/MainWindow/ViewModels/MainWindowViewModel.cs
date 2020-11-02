using System;
using System.Collections.ObjectModel;
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
        private readonly IWindowTracker mainWindowTracker;
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IImageProvider imageProvider;

        private readonly IStartupManager startupManager;
        private readonly IAppArguments appArguments;
        private readonly IMicrophoneControllerEx microphoneController;

        private HotkeyGesture hotkey;
        private HotkeyGesture hotkeyAlt;
        private bool isPushToTalkMode;
        private bool suppressHotkey;
        private MicrophoneLineData microphoneLine;
        private bool showInTaskbar;
        private Visibility trayIconVisibility = Visibility.Visible;
        private WindowState windowState;
        private bool startMinimized;
        private Visibility visibility;

        public MainWindowViewModel(
            [NotNull] IAppArguments appArguments,
            [NotNull] IFactory<IStartupManager, StartupManagerArgs> startupManagerFactory,
            [NotNull] IMicrophoneControllerEx microphoneController,
            [NotNull] IMicSwitchOverlayViewModel overlay,
            [NotNull] IAudioNotificationsManager audioNotificationsManager,
            [NotNull] IFactory<IAudioNotificationSelectorViewModel> audioSelectorFactory,
            [NotNull] IApplicationUpdaterViewModel appUpdater,
            [NotNull] [Dependency(WellKnownWindows.MainWindow)] IWindowTracker mainWindowTracker,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] IComplexHotkeyTracker hotkeyTracker,
            [NotNull] IMicrophoneProvider microphoneProvider,
            [NotNull] IImageProvider imageProvider,
            [NotNull] IViewController viewController,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            var startupManagerArgs = new StartupManagerArgs
            {
                UniqueAppName = $"{appArguments.AppName}{(appArguments.IsDebugMode ? "-debug" : string.Empty)}",
                ExecutablePath = appUpdater.GetLatestExecutable().FullName,
                CommandLineArgs = appArguments.StartupArgs,
                AutostartFlag = appArguments.AutostartFlag
            };
            this.startupManager = startupManagerFactory.Create(startupManagerArgs);

            this.appArguments = appArguments;
            this.microphoneController = microphoneController;

            ApplicationUpdater = appUpdater;
            this.mainWindowTracker = mainWindowTracker;
            this.configProvider = configProvider;
            this.imageProvider = imageProvider;
            this.RaiseWhenSourceValue(x => x.IsActive, mainWindowTracker, x => x.IsActive).AddTo(Anchors);

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
                .Subscribe(x =>
                {
                    IsPushToTalkMode = x;
                    if (isPushToTalkMode)
                    {
                        MuteMicrophoneCommand.Execute(true);
                    }
                }, Log.HandleException)
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

            this.RaiseWhenSourceValue(x => x.RunAtLogin, startupManager, x => x.IsRegistered, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneMuted, microphoneController, x => x.Mute, uiScheduler).AddTo(Anchors);
            ImageProvider = imageProvider;

            microphoneProvider.Microphones
                .ToObservableChangeSet()
                .ObserveOn(uiScheduler)
                .Bind(out var microphones)
                .Subscribe()
                .AddTo(Anchors);
            Microphones = microphones;

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
                .Subscribe(x => microphoneController.LineId = x, Log.HandleUiException)
                .AddTo(Anchors);

            Observable.Merge(
                    configProvider.ListenTo(x => x.MicrophoneLineId).ToUnit(),
                    Microphones.ToObservableChangeSet().ToUnit())
                .Select(_ => configProvider.ActualConfig.MicrophoneLineId)
                .ObserveOn(uiScheduler)
                .Subscribe(configLineId =>
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

            hotkeyTracker
                .WhenAnyValue(x => x.IsActive)
                .ObserveOn(uiScheduler)
                .Subscribe(async isActive =>
                {
                    if (isPushToTalkMode)
                    {
                        await MuteMicrophoneCommandExecuted(!isActive);
                    }
                    else
                    {
                        await MuteMicrophoneCommandExecuted(!MicrophoneMuted);
                    }
                }, Log.HandleUiException)
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
                .Subscribe(x => ShowInTaskbar = x != WindowState.Minimized, Log.HandleUiException)
                .AddTo(Anchors);

            ShowAppCommand = CommandWrapper.Create(
                () =>
                {
                    if (Visibility != Visibility.Visible)
                    {
                        viewController.Show();
                    }
                    else
                    {
                        viewController.Hide();
                    }
                });

            OpenAppDataDirectoryCommand = CommandWrapper.Create(OpenAppDataDirectory);

            ResetOverlayPositionCommand = CommandWrapper.Create(ResetOverlayPositionCommandExecuted);
            
            RunAtLoginToggleCommand = CommandWrapper.Create<bool>(RunAtLoginCommandExecuted);
            MuteMicrophoneCommand = CommandWrapper.Create<bool>(MuteMicrophoneCommandExecuted);
            SelectMicrophoneIconCommand = CommandWrapper.Create(SelectMicrophoneIconCommandExecuted);
            SelectMutedMicrophoneIconCommand = CommandWrapper.Create(SelectMutedMicrophoneIconCommandExecuted);
            ResetMicrophoneIconsCommand = CommandWrapper.Create(ResetMicrophoneIconsCommandExecuted);

            var executingAssemblyName = Assembly.GetExecutingAssembly().GetName();
            Title = $"{(appArguments.IsDebugMode ? "[D]" : "")} {executingAssemblyName.Name} v{executingAssemblyName.Version}";

            WindowState = WindowState.Minimized;
            viewController
                .WhenLoaded
                .Take(1)
                .Select(() => configProvider.ListenTo(y => y.StartMinimized))
                .Switch()
                .Take(1)
                .ObserveOn(uiScheduler)
                .Subscribe(
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
                }, Log.HandleUiException)
                .AddTo(Anchors);
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

        private async Task<byte[]> SelectIcon()
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
            return File.ReadAllBytes(op.FileName);
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
            Log.Debug($"Resetting overlay position, current size: {new Rect(Overlay.Left, Overlay.Top, Overlay.Width, Overlay.Height)}");
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
        }
        
        public IImageProvider ImageProvider { get; }

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