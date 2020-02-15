using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows;
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
using Unity;
using Unity.Resolution;

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
            
            if (AppArguments.Instance.IsDebugMode)
            {
                container.RegisterType<IConfigProvider, PoeEyeConfigProviderInMemory>();
            }
            else
            {
                container.RegisterType<IConfigProvider, ConfigProviderFromFile>();
            }

            container.AddNewExtension<CommonRegistrations>();
            container.AddNewExtension<NativeRegistrations>();
            container.AddNewExtension<WpfCommonRegistrations>();
            container.AddNewExtension<UpdaterRegistrations>();
            
            container.RegisterType<IMicrophoneController, MicrophoneController>();
            container.RegisterSingleton<IMicrophoneProvider, MicrophoneProvider>();
            container.RegisterSingleton<IMicSwitchOverlayViewModel, MicSwitchOverlayViewModel>();
            container.RegisterSingleton<IComplexHotkeyTracker, ComplexHotkeyTracker>();

            InitializeUpdateSettings();

            Log.Debug($"Registrations took {sw.ElapsedMilliseconds:F0}ms");

            var viewController = new ViewController(this);
            sw.Restart();
            DataContext = container.Resolve<MainWindowViewModel>(new DependencyOverride<IViewController>(viewController));
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"MainWindow unloaded");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"MainWindow loaded");
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

        private sealed class ViewController : IViewController
        {
            private readonly Window owner;
            private readonly ISubject<Unit> whenLoaded = new Subject<Unit>();

            public ViewController(Window owner)
            {
                this.owner = owner;
                owner.Loaded += OnLoaded;
                owner.Unloaded += OnUnloaded;
            }

            public IObservable<Unit> WhenLoaded => whenLoaded;

            private void OnUnloaded(object sender, RoutedEventArgs e)
            {
                Log.Debug($"MainWindow unloaded");
            }

            private void OnLoaded(object sender, RoutedEventArgs e)
            {
                Log.Debug($"MainWindow loaded");
                whenLoaded.OnNext(Unit.Default);
            }
        }
    }
}