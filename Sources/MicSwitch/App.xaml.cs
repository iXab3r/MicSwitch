using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using log4net;
using MicSwitch.MainWindow.Models;
using MicSwitch.MainWindow.ViewModels;
using MicSwitch.Prism;
using MicSwitch.Services;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Native.Scaffolding;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Squirrel.Prism;
using PoeShared.Squirrel.Updater;
using PoeShared.Wpf.Scaffolding;
using PoeShared.Wpf.UI.ExceptionViewer;
using ReactiveUI;
using Unity;
using Unity.Resolution;
using Application = System.Windows.Forms.Application;

namespace MicSwitch
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
        private readonly UnityContainer container = new UnityContainer();
        private readonly IAppArguments appArguments;

        public App()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();

                InitializeContainer();
                appArguments = container.Resolve<IAppArguments>();
              
                if (!appArguments.Parse(arguments))
                {
                    SharedLog.Instance.InitializeLogging("Startup", appArguments.AppName);
                    throw new ApplicationException($"Failed to parse command line args: {string.Join(" ", arguments)}");
                }
                
                if (appArguments.IsDebugMode)
                {
                    container.RegisterSingleton<IConfigProvider, PoeEyeConfigProviderInMemory>();
                }
                else
                {
                    container.RegisterSingleton<IConfigProvider, ConfigProviderFromFile>();
                }
                
                InitializeLogging();

                Log.Debug($"Arguments: {arguments.DumpToText()}");
                Log.Debug($"ProcessID: {Process.GetCurrentProcess().Id}");
                Log.Debug($"Parsed args: {appArguments}");
                Log.Debug($"Culture: {Thread.CurrentThread.CurrentCulture}, UICulture: {Thread.CurrentThread.CurrentUICulture}");
                
                Log.Debug($"UI Scheduler: {RxApp.MainThreadScheduler}");
                RxApp.MainThreadScheduler = container.Resolve<IScheduler>(WellKnownSchedulers.UI);
                RxApp.TaskpoolScheduler = TaskPoolScheduler.Default;
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Log.Debug($"New UI Scheduler: {RxApp.MainThreadScheduler}");
                InitializeUpdateSettings();       
            }
            catch (Exception ex)
            {
                Log.HandleException(ex);
                throw;
            }
        }

        private static ILog Log => SharedLog.Instance.Log;

        public CompositeDisposable Anchors { get; } = new CompositeDisposable();
        
        private void InitializeContainer()
        {
            container.AddNewExtension<Diagnostic>();
            container.AddNewExtension<CommonRegistrations>();
            container.AddNewExtension<NativeRegistrations>();
            container.AddNewExtension<WpfCommonRegistrations>();
            container.AddNewExtension<UpdaterRegistrations>();
            
            container.RegisterSingleton<IMicrophoneControllerEx, ComplexMicrophoneController>();
            container.RegisterSingleton<IMicrophoneProvider, MicrophoneProvider>();
            container.RegisterSingleton<IMicSwitchOverlayViewModel, MicSwitchOverlayViewModel>();
            container.RegisterSingleton<IComplexHotkeyTracker, ComplexHotkeyTracker>();
            container.RegisterSingleton<IMainWindowViewModel, MainWindowViewModel>();
            container.RegisterSingleton<IImageProvider, ImageProvider>();
        }
        
        private void InitializeUpdateSettings()
        {
            var updateSourceProvider = container.Resolve<IUpdateSourceProvider>();
            Log.Debug($"Reconfiguring {nameof(UpdateSettingsConfig)}, current update source: {updateSourceProvider.UpdateSource}");
            UpdateSettings.WellKnownUpdateSources.ForEach(x => updateSourceProvider.KnownSources.Add(x));

            if (!updateSourceProvider.UpdateSource.IsValid || !UpdateSettings.WellKnownUpdateSources.Contains(updateSourceProvider.UpdateSource))
            {
                var plusUpdateSource = UpdateSettings.WellKnownUpdateSources.First();
                Log.Info($"Future updates will be provided by {plusUpdateSource} instead of {updateSourceProvider.UpdateSource} (isValid: {updateSourceProvider.UpdateSource.IsValid})");
                updateSourceProvider.UpdateSource = plusUpdateSource;
            }
            else
            {
                Log.Info($"Updates are provided by {updateSourceProvider.UpdateSource}");
            }
        }

        private void SingleInstanceValidationRoutine(bool retryIfAbandoned)
        {
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
                Log.Debug($"Mutex is abandoned {mutexId} (retryIfAbandoned: {retryIfAbandoned})");
                if (retryIfAbandoned)
                {
                    SingleInstanceValidationRoutine(false);
                }
            }
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportCrash(e.ExceptionObject as Exception, "CurrentDomainUnhandledException");
        }

        private void DispatcherOnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportCrash(e.Exception, "DispatcherUnhandledException");
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ReportCrash(e.Exception, "TaskSchedulerUnobservedTaskException");
        }

        private void ReportCrash(Exception exception, string developerMessage = "")
        {
            Log.Error($"Unhandled application exception({developerMessage})", exception);

            AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException;
            Dispatcher.CurrentDispatcher.UnhandledException -= DispatcherOnUnhandledException;

            var reporter = container.Resolve<IExceptionDialogDisplayer>();
            reporter.ShowDialogAndTerminate(exception);
        }

        private void InitializeLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            Dispatcher.CurrentDispatcher.UnhandledException += DispatcherOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            RxApp.DefaultExceptionHandler = SharedLog.Instance.Errors;
            if (appArguments.IsDebugMode)
            {
                SharedLog.Instance.InitializeLogging("Debug", appArguments.AppName);
            }
            else
            {
                SharedLog.Instance.InitializeLogging("Release", appArguments.AppName);
            }

            SharedLog.Instance.AddTraceAppender().AddTo(Anchors);

            var logFileConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
            SharedLog.Instance.LoadLogConfiguration(new FileInfo(logFileConfigPath));
            SharedLog.Instance.Errors.SubscribeSafe(x => ReportCrash(x), Log.HandleUiException).AddTo(Anchors);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            using var sw = new BenchmarkTimer("MainWindow initialization routine", Log);
            Log.Info($"Application startup detected, PID: {Process.GetCurrentProcess().Id}");
            
            SingleInstanceValidationRoutine(true);
            sw.Step("Registering overlay");
            var micSwitchOverlayDependencyName = "MicSwitchOverlayAllWindows";
            container.RegisterOverlayController(micSwitchOverlayDependencyName, micSwitchOverlayDependencyName);
            var matcher = new RegexStringMatcher().AddToWhitelist(".*");
            container.RegisterWindowTracker(micSwitchOverlayDependencyName, matcher);
            var overlayController = container.Resolve<IOverlayWindowController>(micSwitchOverlayDependencyName);
            var overlayViewModelFactory =
                container.Resolve<IFactory<IMicSwitchOverlayViewModel, IOverlayWindowController>>();
            var overlayViewModel = overlayViewModelFactory.Create(overlayController).AddTo(Anchors);
            
            var mainWindow = container.Resolve<MainWindow.Views.MainWindow>();
            Current.MainWindow = mainWindow;
            sw.Step($"Main window view initialized");
            
            var viewController = new WindowViewController(mainWindow);
            var mainWindowViewModel = container.Resolve<IMainWindowViewModel>(
                new DependencyOverride<IWindowViewController>(viewController),
                new DependencyOverride<IOverlayWindowController>(overlayController)).AddTo(Anchors);
            sw.Step($"Main window view model resolved");
            mainWindow.DataContext = mainWindowViewModel;
            sw.Step($"Main window view model assigned");
            mainWindow.Show();
            sw.Step($"Main window shown");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Log.Info($"Application exit detected, PID: {Process.GetCurrentProcess().Id}");
        }

        private void ShowShutdownWarning()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            var window = MainWindow;
            var title = $"{assemblyName.Name} v{assemblyName.Version}";
            var message = "Application is already running !";
            Log.Warn($"Showing shutdown warning for process {Process.GetCurrentProcess().Id}");

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
    }
}