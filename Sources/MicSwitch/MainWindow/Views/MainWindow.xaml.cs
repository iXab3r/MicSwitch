using System.Diagnostics;
using Common.Logging;
using MicSwitch.MainWindow.Models;
using MicSwitch.MainWindow.ViewModels;
using MicSwitch.Modularity;
using MicSwitch.Updater;
using PoeEye;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using Unity;
using Unity.Lifetime;

namespace MicSwitch.MainWindow.Views
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindow));

        private readonly UnityContainer container = new UnityContainer();

        public MainWindow()
        {
            Log.Debug($"Initializing MainWindow for process {AppArguments.Instance.ProcessId}");
            var sw = Stopwatch.StartNew();
            InitializeComponent();
            Log.Debug($"BAML loaded in {sw.ElapsedMilliseconds:F0}ms");
            sw.Restart();
            
            container.RegisterInstance(AppArguments.Instance, new ContainerControlledLifetimeManager());
            if (AppArguments.Instance.IsDebugMode)
            {
                container.RegisterType<IConfigProvider, PoeEyeConfigProviderInMemory>();
            }
            else
            {
                container.RegisterType<IConfigProvider, ConfigProviderFromFile>();
            }

            container.RegisterType<IMicrophoneController, MicrophoneController>();
            container.RegisterType<IApplicationUpdaterModel, ApplicationUpdaterModel>();
            container.RegisterSingleton<IMicSwitchOverlayViewModel, MicSwitchOverlayViewModel>();

            container.AddExtension(new CommonRegistrations());
            Log.Debug($"Registrations took {sw.ElapsedMilliseconds:F0}ms");
            sw.Restart();
            
            DataContext = container.Resolve<MainWindowViewModel>();
            Log.Debug($"MainWindow resolved in {sw.ElapsedMilliseconds:F0}ms");
            sw.Restart();
            
            var micSwitchOverlayDependencyName = "MicSwitchOverlayAllWindows";
            container.RegisterOverlayController(micSwitchOverlayDependencyName, micSwitchOverlayDependencyName);

            var matcher = new RegexStringMatcher().AddToWhitelist(".*");
            container.RegisterWindowTracker(micSwitchOverlayDependencyName, matcher);

            var overlayController = container.Resolve<IOverlayWindowController>(micSwitchOverlayDependencyName);
            var overlayViewModelFactory =
                container.Resolve<IFactory<IMicSwitchOverlayViewModel, IOverlayWindowController>>();
            var overlayViewModel = overlayViewModelFactory.Create(overlayController);
            overlayController.RegisterChild(overlayViewModel);
            Log.Debug($"Overlays loaded in {sw.ElapsedMilliseconds:F0}ms");
        }
    }
}