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
        private readonly IMicrophoneController microphoneController;

        private bool isVisible;

        public MicSwitchOverlayViewModel(
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] IImageProvider imageProvider,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
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
            WhenLoaded
                .Take(1)
                .Select(x => configProvider.WhenChanged)
                .Switch()
                .ObserveOn(uiScheduler)
                .Subscribe(ApplyConfig)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.IsLocked)
                .Subscribe(isLocked => OverlayMode = isLocked ? OverlayMode.Transparent : OverlayMode.Layered)
                .AddTo(Anchors);

            configProvider.ListenTo(x => x.MicrophoneLineId)
                .ObserveOn(uiScheduler)
                .Subscribe(lineId => { microphoneController.LineId = lineId; })
                .AddTo(Anchors);

            this.RaiseWhenSourceValue(x => x.Mute, microphoneController, x => x.Mute).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneImage, imageProvider, x => x.ActiveMicrophoneImage).AddTo(Anchors);
            
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
                    this.ObservableForProperty(x => x.Left, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Top, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Width, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.Height, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsVisible, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.IsLocked, skipInitial: true).ToUnit())
                .SkipUntil(WhenLoaded)
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .Subscribe(SaveConfig, Log.HandleUiException)
                .AddTo(Anchors);
        }

        public bool IsVisible
        {
            get => isVisible;
            set => this.RaiseAndSetIfChanged(ref isVisible, value);
        }

        public bool Mute => microphoneController.Mute ?? false;

        public ImageSource MicrophoneImage => imageProvider.ActiveMicrophoneImage;

        public ICommand ToggleLockStateCommand { get; }
        
        private void ApplyConfig(MicSwitchConfig config)
        {
            base.ApplyConfig(config);

            IsVisible = config.IsVisible;
        }

        private void SaveConfig()
        {
            var config = configProvider.ActualConfig.CloneJson();
            SavePropertiesToConfig(config);

            config.IsVisible = IsVisible;

            configProvider.Save(config);
        }
    }
}