using System;
using System.IO;
using System.Linq;
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
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static ILog Log => SharedLog.Instance.Log;

        public App()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();

                if (!AppArguments.Parse(arguments))
                {
                    SharedLog.Instance.InitializeLogging("Startup");
                    throw new ApplicationException($"Failed to parse command line args: {string.Join(" ", arguments)}");
                }


                InitializeLogging();
                Log.Debug($"[App..ctor] Arguments: {arguments.DumpToText()}");
                Log.Debug($"[App..ctor] Parsed args: {AppArguments.Instance.DumpToText()}");
                Log.Debug($"[App..ctor] Culture: {Thread.CurrentThread.CurrentCulture}, UICulture: {Thread.CurrentThread.CurrentUICulture}");

                RxApp.SupportsRangeNotifications = false; //FIXME DynamicData (as of v4.11) does not support RangeNotifications
                Log.Debug($"[App..ctor] UI Scheduler: {RxApp.MainThreadScheduler}");
                RxApp.MainThreadScheduler = DispatcherScheduler.Current;
                RxApp.TaskpoolScheduler = TaskPoolScheduler.Default;
                App.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Log.Debug($"[App..ctor] New UI Scheduler: {RxApp.MainThreadScheduler}");
            }
            catch (Exception ex)
            {
                Log.HandleException(ex);
                throw;
            }
        }
        
        private void SingleInstanceValidationRoutine()
        {
            var mutexId = $"MicSwitch{(AppArguments.Instance.IsDebugMode ? "DEBUG" : "RELEASE")}{{567EBFFF-E391-4B38-AC85-469978EB37C4}}";
            Log.Debug($"[App] Acquiring mutex {mutexId}...");
            var mutex = new Mutex(true, mutexId);
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                Log.Debug($"[App] Mutex {mutexId} was successfully acquired");

                AppDomain.CurrentDomain.DomainUnload += delegate
                {
                    Log.Debug($"[App.DomainUnload] Detected DomainUnload, disposing mutex {mutexId}");
                    mutex.ReleaseMutex();
                    Log.Debug("[App.DomainUnload] Mutex was successfully disposed");
                };
            }
            else
            {
                Log.Warn($"[App] Application is already running, mutex: {mutexId}");
                ShowShutdownWarning();
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
                SharedLog.Instance.InitializeLogging("Debug");
            }
            else
            {
                SharedLog.Instance.InitializeLogging("Release");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Log.Debug("Application startup detected");

            SingleInstanceValidationRoutine();

            Log.Info("Initializing bootstrapper...");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Log.Debug("Application exit detected");
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
    }
}