using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Services;
using Size = System.Drawing.Size;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class MicSwitchOverlayViewModel : OverlayViewModelBase, IMicSwitchOverlayViewModel
    {
        private static readonly Binder<MicSwitchOverlayViewModel> Binder = new();
        private static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromMilliseconds(250);
        
        private readonly IConfigProvider<MicSwitchOverlayConfig> configProvider;
        private readonly IImageProvider imageProvider;
        private readonly IOverlayWindowController overlayWindowController;

        static MicSwitchOverlayViewModel()
        {
            Binder.BindIf(x => x.MicrophoneDeviceController != null && x.MicrophoneDeviceController.Mute.HasValue, x => x.MicrophoneDeviceController.Mute.Value)
                .Else(x => false)
                .To(x => x.MicrophoneMute);
            
            Binder.BindIf(x => x.OutputDeviceController != null, x => x.OutputDeviceController.VolumePercent)
                .Else(x => default)
                .To(x => x.OutputVolume);
            Binder.BindIf(x => x.OutputDeviceController != null && x.OutputDeviceController.Mute == true, x => PackIconKind.VolumeMute)
                .ElseIf(x => x.OutputVolume == 0, x => PackIconKind.VolumeOff)
                .ElseIf(x => x.OutputVolume > 0.25, x => PackIconKind.VolumeLow)
                .ElseIf(x => x.OutputVolume > 0.5, x => PackIconKind.VolumeMedium)
                .ElseIf(x => x.OutputVolume > 0.75, x => PackIconKind.VolumeHigh)
                .Else(x => PackIconKind.None)
                .To(x => x.OutputVolumeKind);
        }

        public MicSwitchOverlayViewModel(
            IOverlayWindowController overlayWindowController,
            IConfigProvider<MicSwitchOverlayConfig> configProvider,
            IImageProvider imageProvider,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            this.overlayWindowController = overlayWindowController;
            this.configProvider = configProvider;
            this.imageProvider = imageProvider;
            OverlayMode = OverlayMode.Transparent;
            MinSize = new Size(20, 20);
            MaxSize = new Size(300, 300);
            DefaultSize = new Size(120, 120);
            SizeToContent = SizeToContent.Manual;
            TargetAspectRatio = MinSize.Width / MinSize.Height;
            IsUnlockable = true;
            Title = string.Empty;
            IsEnabled = true;
            EnableHeader = false;

            this.WhenAnyValue(x => x.IsLocked)
                .SubscribeSafe(isLocked => OverlayMode = isLocked ? OverlayMode.Transparent : OverlayMode.Layered, Log.HandleUiException)
                .AddTo(Anchors);

            this.RaiseWhenSourceValue(x => x.IsEnabled, overlayWindowController, x => x.IsEnabled).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneImage, imageProvider, x => x.MicrophoneImage, uiScheduler).AddTo(Anchors);
            
            ToggleLockStateCommand = CommandWrapper.Create(
                () =>
                {
                    if (IsLocked && UnlockWindowCommand.CanExecute(null))
                    {
                        UnlockWindowCommand.Execute(null);
                    }
                    else if (!IsLocked && LockWindowCommand.CanExecute(null))
                    {
                        LockWindowCommand.Execute(null);
                    }
                    else
                    {
                        throw new ApplicationException($"Something went wrong - invalid Overlay Lock state: {new {IsLocked, IsUnlockable, CanUnlock = UnlockWindowCommand.CanExecute(null), CanLock = LockWindowCommand.CanExecute(null)  }}");
                    }
                });

            this.WhenAnyValue(x => x.IsEnabled)
                .Where(x => !IsEnabled && !IsLocked)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() => LockWindowCommand.Execute(null), Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.OverlayWindow)
                .Where(x => x != null)
                .SubscribeSafe(x => x.LogWndProc("MicOverlay").AddTo(Anchors), Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.OverlayVisibilityMode, x => x.MicrophoneMute)
                .Select(_ =>
                {
                    return OverlayVisibilityMode switch
                    {
                        OverlayVisibilityMode.Always => true,
                        OverlayVisibilityMode.Never => false,
                        OverlayVisibilityMode.WhenMuted => MicrophoneMute,
                        OverlayVisibilityMode.WhenUnmuted => !MicrophoneMute,
                        _ => throw new ArgumentOutOfRangeException(nameof(OverlayVisibilityMode), OverlayVisibilityMode, "Unknown visibility mode")
                    };
                })
                .DistinctUntilChanged()
                .ObserveOn(uiScheduler)
                .SubscribeSafe(x => IsEnabled = x, Log.HandleUiException)
                .AddTo(Anchors);
            
            WhenLoaded
                .Take(1)
                .SubscribeSafe(_ =>
                {
                    configProvider
                        .WhenChanged
                        .ObserveOn(uiScheduler)
                        .SubscribeSafe(LoadConfig, Log.HandleUiException)
                        .AddTo(Anchors);
                    
                    Observable.Merge(
                            this.ObservableForProperty(x => x.NativeBounds, skipInitial: true).ToUnit(),
                            this.ObservableForProperty(x => x.Opacity, skipInitial: true).ToUnit(),
                            this.ObservableForProperty(x => x.OverlayVisibilityMode, skipInitial: true).ToUnit(),
                            this.ObservableForProperty(x => x.IsEnabled, skipInitial: true).ToUnit(),
                            this.ObservableForProperty(x => x.IsLocked, skipInitial: true).ToUnit())
                        .SkipUntil(WhenLoaded)
                        .Throttle(ConfigThrottlingTimeout)
                        .ObserveOn(uiScheduler)
                        .SubscribeSafe(SaveConfig, Log.HandleUiException)
                        .AddTo(Anchors);
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            Binder.Attach(this).AddTo(Anchors);
        }

        public double? OutputVolume { get; [UsedImplicitly] private set; }
        public PackIconKind OutputVolumeKind { get; [UsedImplicitly] private set; }

        public bool IsEnabled
        {
            get => overlayWindowController.IsEnabled;
            set => overlayWindowController.IsEnabled = value;
        }

        public bool MicrophoneMute { get; [UsedImplicitly] private set; }

        public OverlayVisibilityMode OverlayVisibilityMode { get; set; }
        public IMMDeviceController MicrophoneDeviceController { get; set; }
        public IMMDeviceController OutputDeviceController { get; set; }

        public ImageSource MicrophoneImage => imageProvider.MicrophoneImage;

        public ICommand ToggleLockStateCommand { get; }

        private void SaveConfig()
        {
            var config = configProvider.ActualConfig.CloneJson();
            SavePropertiesToConfig(config);
            config.OverlayVisibilityMode = OverlayVisibilityMode;
            configProvider.Save(config);
        }

        private void LoadConfig(MicSwitchOverlayConfig config)
        {
            ApplyConfig(config);
            OverlayVisibilityMode = config.OverlayVisibilityMode;
        }
    }
}