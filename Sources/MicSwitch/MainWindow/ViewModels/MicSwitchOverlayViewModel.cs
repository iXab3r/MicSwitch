using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
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
        private readonly IConfigProvider<MicSwitchConfig> configProvider;
        private readonly IMicrophoneController microphoneController;

        private bool isVisible;
        private double listScaleFactor;

        public MicSwitchOverlayViewModel(
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            this.microphoneController = microphoneController;
            this.configProvider = configProvider;
            OverlayMode = OverlayMode.Transparent;
            MinSize = new Size(150, 150);
            MaxSize = new Size(300, 300);
            SizeToContent = SizeToContent.Height;
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
        }

        public bool IsVisible
        {
            get => isVisible;
            set => this.RaiseAndSetIfChanged(ref isVisible, value);
        }

        public bool Mute => microphoneController.Mute ?? false;

        public ICommand ToggleLockStateCommand { get; }
        
        public double ListScaleFactor
        {
            get => listScaleFactor;
            set => this.RaiseAndSetIfChanged(ref listScaleFactor, value);
        }

        private void ApplyConfig(MicSwitchConfig config)
        {
            base.ApplyConfig(config);

            IsVisible = config.IsVisible;
            ListScaleFactor = config.ScaleFactor;
        }

        protected override void LockWindowCommandExecuted()
        {
            base.LockWindowCommandExecuted();

            var config = configProvider.ActualConfig.CloneJson();
            SavePropertiesToConfig(config);

            config.IsVisible = IsVisible;
            config.ScaleFactor = ListScaleFactor;

            configProvider.Save(config);
        }
    }
}