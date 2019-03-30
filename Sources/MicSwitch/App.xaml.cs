using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Common.Logging;
using PoeEye;
using PoeShared;
using PoeShared.Scaffolding;
using ReactiveUI;

namespace MicSwitch
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
        
        public App()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                AppArguments.Instance.AppName = "MicSwitch";

                if (!AppArguments.Parse(arguments))
                {
                    SharedLog.Instance.InitializeLogging("Startup", AppArguments.Instance.AppName);
                    throw new ApplicationException($"Failed to parse command line args: {string.Join(" ", arguments)}");
                }

                InitializeLogging();

                Log.Debug($"Arguments: {arguments.DumpToText()}");
                Log.Debug($"ProcessID: {Process.GetCurrentProcess().Id}");
                Log.Debug($"Parsed args: {AppArguments.Instance}");
                Log.Debug($"Culture: {Thread.CurrentThread.CurrentCulture}, UICulture: {Thread.CurrentThread.CurrentUICulture}");

                SingleInstanceValidationRoutine(true);
                
                RxApp.SupportsRangeNotifications = false; //FIXME DynamicData (as of v4.11) does not support RangeNotifications
                Log.Debug($"UI Scheduler: {RxApp.MainThreadScheduler}");
                RxApp.MainThreadScheduler = DispatcherScheduler.Current;
                RxApp.TaskpoolScheduler = TaskPoolScheduler.Default;
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Log.Debug($"New UI Scheduler: {RxApp.MainThreadScheduler}");
            }
            catch (Exception ex)
            {
                Log.HandleException(ex);
                throw;
            }
        }

        private static ILog Log => SharedLog.Instance.Log;

        private void SingleInstanceValidationRoutine(bool retryIfAbandoned)
        {
            var mutexId = $"MicSwitch{(AppArguments.Instance.IsDebugMode ? "DEBUG" : "RELEASE")}{{567EBFFF-E391-4B38-AC85-469978EB37C4}}";
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
            Current.Dispatcher.UnhandledException -= DispatcherOnUnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException;
        }

        private void InitializeLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            Current.Dispatcher.UnhandledException += DispatcherOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            RxApp.DefaultExceptionHandler = SharedLog.Instance.Errors;
            if (AppArguments.Instance.IsDebugMode)
            {
                SharedLog.Instance.InitializeLogging("Debug", AppArguments.Instance.AppName);
            }
            else
            {
                SharedLog.Instance.InitializeLogging("Release", AppArguments.Instance.AppName);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Log.Info($"Application startup detected, PID: {Process.GetCurrentProcess().Id}");
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