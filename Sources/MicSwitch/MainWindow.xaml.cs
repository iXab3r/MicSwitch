using System.Collections.ObjectModel;
using System.Windows.Forms;
using DynamicData;
using PoeEye;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using Unity.Resolution;

namespace MicSwitch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow 
    {
        private readonly UnityContainer container = new UnityContainer();
        
        public MainWindow()
        {
            InitializeComponent();

            AppArguments.Instance.IsDebugMode = true;
            container.RegisterType<IConfigProvider, PoeEyeConfigProviderInMemory>();
            container.RegisterType<IMicrophoneController, MicrophoneController>();
            container.RegisterSingleton<IMicSwitchOverlayViewModel, MicSwitchOverlayViewModel>();
            
            container.AddExtension(new CommonRegistrations());

            var micSwitchOverlayDependencyName = "MicSwitchOverlay";
            container.RegisterOverlayController(micSwitchOverlayDependencyName, micSwitchOverlayDependencyName);

            var matcher = new RegexStringMatcher()
                .AddToWhitelist(".*");
            container
                .RegisterType<IWindowTracker>(
                    micSwitchOverlayDependencyName,
                    new ContainerControlledLifetimeManager(),
                    new InjectionFactory(unity => unity.Resolve<WindowTracker>(new DependencyOverride<IStringMatcher>(matcher))));

            var overlayController = container.Resolve<IOverlayWindowController>(micSwitchOverlayDependencyName);
            var overlayViewModelFactory = container.Resolve<IFactory<IMicSwitchOverlayViewModel, IOverlayWindowController>>();
            var overlayViewModel = overlayViewModelFactory.Create(overlayController);
            overlayController.RegisterChild(overlayViewModel);

            this.DataContext = container.Resolve<MainWindowViewModel>();
        }
    }
}