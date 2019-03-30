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
        private readonly UnityContainer container = new UnityContainer();

        public MainWindow()
        {
            InitializeComponent();

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
            DataContext = container.Resolve<MainWindowViewModel>();

            var micSwitchOverlayDependencyName = "MicSwitchOverlayAllWindows";
            container.RegisterOverlayController(micSwitchOverlayDependencyName, micSwitchOverlayDependencyName);

            var matcher = new RegexStringMatcher().AddToWhitelist(".*");
            container.RegisterWindowTracker(micSwitchOverlayDependencyName, matcher);

            var overlayController = container.Resolve<IOverlayWindowController>(micSwitchOverlayDependencyName);
            var overlayViewModelFactory =
                container.Resolve<IFactory<IMicSwitchOverlayViewModel, IOverlayWindowController>>();
            var overlayViewModel = overlayViewModelFactory.Create(overlayController);
            overlayController.RegisterChild(overlayViewModel);
        }
    }
}