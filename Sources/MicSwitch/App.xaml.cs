using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using MicSwitch.MainWindow.Models;
using MicSwitch.MainWindow.ViewModels;
using MicSwitch.Modularity;
using MicSwitch.Prism;
using MicSwitch.Services;
using PoeShared.Squirrel.Prism;
using PoeShared.Squirrel.Updater;
using Unity.Resolution;

namespace MicSwitch
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class App : ApplicationBase
    {
        public readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);

        private void InitializeContainer()
        {
            Container.AddNewExtensionIfNotExists<UpdaterRegistrations>();
            
            Container.RegisterType<IHotkeyEditorViewModel, HotkeyEditorViewModel>()
                     .RegisterSingleton<IMMDeviceControllerEx, ComplexMMDeviceController>()
                     .RegisterSingleton<IMicSwitchOverlayViewModel, MicSwitchOverlayViewModel>()
                     .RegisterSingleton<IComplexHotkeyTracker, ComplexHotkeyTracker>()
                     .RegisterSingleton<IMicrophoneControllerViewModel, MicrophoneControllerViewModel>()
                     .RegisterSingleton<IMainWindowViewModel, MainWindowViewModel>()
                     .RegisterSingleton<IImageProvider, ImageProvider>()
                     .RegisterSingleton<IConfigProvider, ConfigProviderFromFile>();
        }
        
        private void InitializeUpdateSettings()
        {
            var updateSourceProvider = Container.Resolve<IUpdateSourceProvider>();
            Log.Debug($"Reconfiguring {nameof(UpdateSettingsConfig)}, current update source: {updateSourceProvider.UpdateSource}");
            updateSourceProvider.KnownSources = UpdateSettings.WellKnownUpdateSources;
            Log.Debug(() => $"Update source provider {updateSourceProvider}, active: {updateSourceProvider.UpdateSource}, known sources: {updateSourceProvider.KnownSources.DumpToString()}");
        }

        private void SingleInstanceValidationRoutine(bool retryIfAbandoned)
        {
            var appArguments = Container.Resolve<IAppArguments>();
            var mutexId = $"MicSwitch{(appArguments.IsDebugMode ? "DEBUG" : "RELEASE")}{{567EBFFF-E391-4B38-AC85-469978EB37C4}}";
            Log.Debug($"Acquiring mutex {mutexId} (retryIfAbandoned: {retryIfAbandoned})...");
            try
            {
                var mutex = new Mutex(true, mutexId);
                if (mutex.WaitOne(StartupTimeout))
                {
                    Log.Debug($"Mutex {mutexId} was successfully acquired");

                    AppDomain.CurrentDomain.DomainUnload += delegate
                    {
                        Log.Debug($"[App.DomainUnload] Detected DomainUnload, disposing mutex {mutexId}");
                        mutex.ReleaseMutex();
                        Log.Debug("[App.DomainUnload] Mutex was successfully disposed");
                    };
                }
                else
                {
                    Log.Error($"Application is already running, mutex: {mutexId}");
                    ShowShutdownWarning();
                }
            }
            catch (AbandonedMutexException ex)
            {
                Log.Debug($"Mutex is abandoned {mutexId} (retryIfAbandoned: {retryIfAbandoned})", ex);
                if (retryIfAbandoned)
                {
                    SingleInstanceValidationRoutine(false);
                }
            }
        }

        private void ShowShutdownWarning()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            var window = MainWindow;
            var title = $"{assemblyName.Name} v{assemblyName.Version}";
            var message = "Application is already running !";
            if (window != null)
            {
                MessageBox.Show(window, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Log.Warn("Shutting down...");
            Environment.Exit(0);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitializeContainer();

            using var sw = new BenchmarkTimer("MainWindow initialization routine", Log);
            Log.Info($"Application startup detected, PID: {Process.GetCurrentProcess().Id}");

            Log.Debug("Resolving squirrel events handler");
            var squirrelEventsHandler = Container.Resolve<ISquirrelEventsHandler>();
            Log.Debug(() => $"Resolved squirrel events handler: {squirrelEventsHandler}");

            SingleInstanceValidationRoutine(true);
            
            var configProvider = Container.Resolve<IConfigProvider>();
            if (configProvider is ConfigProviderFromFile fromFile)
            {
                Log.Debug("Loading initial configuration");
                var defaultConfigProviderStrategy = Container.Resolve<UseDefaultIfFailureConfigProviderStrategy>();
                fromFile.RegisterStrategy(defaultConfigProviderStrategy);
                fromFile.Reload();
            }
            Log.Debug("Initial configuration loaded");
            
            InitializeUpdateSettings();
            
            sw.Step("Actualizing configuration format");
            Log.Debug("Initializing config provider");
            var hotkeyConfigProvider = Container.Resolve<IConfigProvider<MicSwitchHotkeyConfig>>();
            var overlayConfigProvider = Container.Resolve<IConfigProvider<MicSwitchOverlayConfig>>();
            var mainConfigProvider = Container.Resolve<IConfigProvider<MicSwitchConfig>>();
            ActualizeConfig(mainConfigProvider, hotkeyConfigProvider);
            ActualizeConfig(mainConfigProvider, overlayConfigProvider);
            ActualizeConfig(mainConfigProvider);
            
            sw.Step("Registering overlay");
            var overlayController = Container.Resolve<IOverlayWindowController>(WellKnownWindows.AllWindows);
            var overlayViewModelFactory = Container.Resolve<IFactory<IMicSwitchOverlayViewModel, IOverlayWindowController>>();
            var overlayViewModel = overlayViewModelFactory.Create(overlayController).AddTo(Anchors);
            
            var mainWindow = Container.Resolve<MainWindow.Views.MainWindow>();
            Current.MainWindow = mainWindow;
            sw.Step($"Main window view initialized");
            
            var viewController = new WindowViewController(mainWindow);
            var mainWindowViewModel = Container.Resolve<IMainWindowViewModel>(
                new DependencyOverride<IWindowViewController>(viewController),
                new DependencyOverride<IOverlayWindowController>(overlayController)).AddTo(Anchors);
            sw.Step($"Main window view model resolved");
            mainWindow.DataContext = mainWindowViewModel;
            sw.Step($"Main window view model assigned");
            mainWindow.Show();
            sw.Step($"Main window shown");
        }

        private void ActualizeConfig(IConfigProvider<MicSwitchConfig> mainConfigProvider)
        {
            Log.Debug($"Actualizing configuration format of {mainConfigProvider}");
            var config = mainConfigProvider.ActualConfig.CloneJson();
            if (config.Notification != null)
            {
                config.Notifications = new TwoStateNotification
                {
                    // initial configuration contained reversed values
                    Off = config.Notification.Value.On,
                    On = config.Notification.Value.Off
                };
                config.Notification = null;
            }
            mainConfigProvider.Save(config);
            Log.Debug("Config format updated successfully");
        }
        
        private void ActualizeConfig(IConfigProvider<MicSwitchConfig> mainConfigProvider, IConfigProvider<MicSwitchHotkeyConfig> hotkeyConfigProvider)
        {
            Log.Debug($"Actualizing configuration format of {hotkeyConfigProvider}");

            var mainConfig = mainConfigProvider.ActualConfig.CloneJson();
            if (mainConfig.SuppressHotkey == null)
            {
                Log.Debug("Main configuration is up-to-date");
                return;
            } 
            
            Log.Warn($"Main configuration is obsolete, converting to a newer format: { new { mainConfig.MicrophoneHotkey, mainConfig.MicrophoneHotkeyAlt, mainConfig.SuppressHotkey } }");
            var config = hotkeyConfigProvider.ActualConfig.CloneJson();
            config.Hotkey = new HotkeyConfig()
            {
                Key = mainConfig.MicrophoneHotkey,
                AlternativeKey = mainConfig.MicrophoneHotkeyAlt,
                Suppress = mainConfig.SuppressHotkey ?? true
            };
            if (mainConfig.MuteMode != null)
            {
                config.MuteMode = mainConfig.MuteMode.Value;
            }
            hotkeyConfigProvider.Save(config);
            
            mainConfig.MicrophoneHotkey = null;
            mainConfig.MicrophoneHotkeyAlt = null;
            mainConfig.SuppressHotkey = null;
            mainConfig.MuteMode = null;
            mainConfigProvider.Save(mainConfig);
            Log.Debug("Config format updated successfully");
        }
        
        private void ActualizeConfig(IConfigProvider<MicSwitchConfig> mainConfigProvider, IConfigProvider<MicSwitchOverlayConfig> overlayConfigProvider)
        {
            Log.Debug($"Actualizing configuration format of {overlayConfigProvider}");

            var mainConfig = mainConfigProvider.ActualConfig.CloneJson();
            if (mainConfig.OverlayBounds == null)
            {
                Log.Debug("Main configuration is up-to-date");
                return;
            }

            Log.Warn($"Main configuration is obsolete, converting to a newer format: { new { mainConfig.OverlayBounds, mainConfig.OverlayEnabled } }");
            var config = overlayConfigProvider.ActualConfig.CloneJson();
            if (mainConfig.OverlayBounds != null)
            {
                config.OverlayBounds = mainConfig.OverlayBounds.Value;
            }
            if (mainConfig.OverlayEnabled != null)
            {
                config.OverlayVisibilityMode = mainConfig.OverlayEnabled == false ? OverlayVisibilityMode.Never : OverlayVisibilityMode.Always;
            }
            if (mainConfig.OverlayOpacity != null)
            {
                config.OverlayOpacity = mainConfig.OverlayOpacity.Value;
            }
            if (mainConfig.MicrophoneIcon != null)
            {
                config.MicrophoneIcon = mainConfig.MicrophoneIcon;
            }
            if (mainConfig.MutedMicrophoneIcon != null)
            {
                config.MutedMicrophoneIcon = mainConfig.MutedMicrophoneIcon;
            }
            overlayConfigProvider.Save(config);
            
            mainConfig.OverlayBounds = null;
            mainConfig.OverlayEnabled = null;
            mainConfig.OverlayOpacity = null;
            mainConfig.OverlayLocation = null;
            mainConfig.OverlaySize = null;
            mainConfig.MicrophoneIcon = null;
            mainConfig.MutedMicrophoneIcon = null;
            mainConfigProvider.Save(mainConfig);
            Log.Debug("Config format updated successfully");
        }
    }
}