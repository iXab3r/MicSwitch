using System;
using System.Reactive.Linq;
using System.Windows;
using JetBrains.Annotations;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Scaffolding;
using ReactiveUI;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class MicSwitchOverlayViewModel : OverlayViewModelBase, IMicSwitchOverlayViewModel
    {
        private readonly IMicrophoneController microphoneController;
        private readonly IConfigProvider<MicSwitchConfig> configProvider;

        private bool isVisible;
        private double listScaleFactor;
        
        public MicSwitchOverlayViewModel(
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider)
        {
            this.microphoneController = microphoneController;
            this.configProvider = configProvider;
            this.OverlayMode = OverlayMode.Transparent;
            this.MinSize = new Size(100,100);
            this.MaxSize = new Size(300,300);
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.IsUnlockable = true;
            this.Title = "MicSwitch";
            
            WhenLoaded
                .Take(1)
                .Select(x => configProvider.WhenChanged)
                .Switch()
                .Subscribe(ApplyConfig)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.IsLocked)
                .Subscribe(isLocked => OverlayMode = isLocked ? OverlayMode.Transparent : OverlayMode.Layered)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.MicrophoneLineId)
                .Subscribe(lineId =>
                {
                    microphoneController.LineId = lineId;
                })
                .AddTo(Anchors);
            
            this.BindPropertyTo(x => x.Mute, microphoneController, x => x.Mute).AddTo(Anchors);
        }

        public bool Mute => microphoneController.Mute ?? false;
        
        public bool IsVisible
        {
            get => isVisible;
            set => this.RaiseAndSetIfChanged(ref isVisible, value);
        }

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

            var config = configProvider.ActualConfig;
            SavePropertiesToConfig(config);

            config.IsVisible = IsVisible;
            config.ScaleFactor = ListScaleFactor;

            configProvider.Save(config);
        }
    }
}