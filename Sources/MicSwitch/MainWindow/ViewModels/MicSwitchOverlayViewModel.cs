using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using JetBrains.Annotations;
using log4net;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Services;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;
using ReactiveUI;
using Unity;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class MicSwitchOverlayViewModel : OverlayViewModelBase, IMicSwitchOverlayViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MicSwitchOverlayViewModel));

        private static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromMilliseconds(250);
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IImageProvider imageProvider;
        private readonly IOverlayWindowController overlayWindowController;
        private readonly IMicrophoneControllerEx microphoneController;

        public MicSwitchOverlayViewModel(
            [NotNull] IOverlayWindowController overlayWindowController,
            [NotNull] IMicrophoneControllerEx microphoneController,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] IImageProvider imageProvider,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            this.overlayWindowController = overlayWindowController;
            this.microphoneController = microphoneController;
            this.configProvider = configProvider;
            this.imageProvider = imageProvider;
            OverlayMode = OverlayMode.Transparent;
            MinSize = new Size(120, 120);
            MaxSize = new Size(300, 300);
            SizeToContent = SizeToContent.Manual;
            TargetAspectRatio = MinSize.Width / MinSize.Height;
            IsUnlockable = true;
            Title = "MicSwitch";
            IsEnabled = true;
            
            WhenLoaded
                .Take(1)
                .Select(_ => configProvider.WhenChanged)
                .Switch()
                .ObserveOn(uiScheduler)
                .SubscribeSafe(ApplyConfig, Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.IsLocked)
                .SubscribeSafe(isLocked => OverlayMode = isLocked ? OverlayMode.Transparent : OverlayMode.Layered, Log.HandleUiException)
                .AddTo(Anchors);

            configProvider.ListenTo(x => x.MicrophoneLineId)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(lineId => microphoneController.LineId = lineId, Log.HandleUiException)
                .AddTo(Anchors);

            this.RaiseWhenSourceValue(x => x.IsEnabled, overlayWindowController, x => x.IsEnabled).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.Mute, microphoneController, x => x.Mute, uiScheduler).AddTo(Anchors);
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
            
            Observable.Merge(
                    this.ObservableForProperty(x => x.NativeBounds, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsEnabled, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsLocked, skipInitial: true).ToUnit())
                .SkipUntil(WhenLoaded)
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(SaveConfig, Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.IsEnabled)
                .Where(x => !IsEnabled && !IsLocked)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() => LockWindowCommand.Execute(null), Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.OverlayWindow)
                .Where(x => x != null)
                .SubscribeSafe(x => x.LogWndProc("MicOverlay").AddTo(Anchors), Log.HandleUiException)
                .AddTo(Anchors);
        }

        public bool IsEnabled
        {
            get => overlayWindowController.IsEnabled;
            set => overlayWindowController.IsEnabled = value;
        }

        public bool Mute => microphoneController.Mute ?? false;

        public ImageSource MicrophoneImage => imageProvider.MicrophoneImage;

        public ICommand ToggleLockStateCommand { get; }
        
        private void ApplyConfig(MicSwitchConfig config)
        {
            base.ApplyConfig(config);
            IsEnabled = config.OverlayEnabled;
        }

        private void SaveConfig()
        {
            var config = configProvider.ActualConfig.CloneJson();
            SavePropertiesToConfig(config);

            config.OverlayEnabled = IsEnabled;
            configProvider.Save(config);
        }
    }
}