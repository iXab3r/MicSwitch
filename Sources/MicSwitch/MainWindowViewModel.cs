using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using JetBrains.Annotations;
using PoeShared.Native;
using PoeShared.Scaffolding;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Common.Logging;
using NAudio.Mixer;
using PoeEye;
using PoeShared;
using PoeShared.Audio;
using PoeShared.Modularity;
using Prism.Commands;
using ReactiveUI;
using Application = System.Windows.Application;

namespace MicSwitch
{
    internal class MainWindowViewModel : DisposableReactiveObject
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindowViewModel));

        private readonly IMicrophoneController microphoneController;
        private readonly SerialDisposable buttonModeAnchors = new SerialDisposable();
        
        private MicrophoneLineData microphoneLine;
        private bool isPushToTalkMode;
        private WindowState windowState;
        private Visibility trayIconVisibility;
        private bool showInTaskbar;

        public MainWindowViewModel(
            [NotNull] IKeyboardEventsSource eventSource,
            [NotNull] IMicrophoneController microphoneController,
            [NotNull] IMicSwitchOverlayViewModel overlay,
            [NotNull] IAudioNotificationsManager audioNotificationsManager,
            [NotNull] IConfigProvider<MicSwitchConfig> configProvider)
        {
            this.microphoneController = microphoneController;
            Overlay = overlay;
            
            this.BindPropertyTo(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent).AddTo(Anchors);
            this.BindPropertyTo(x => x.MicrophoneMuted, microphoneController, x => x.Mute).AddTo(Anchors);

            this.WhenAnyValue(x => x.MicrophoneMuted)
                .DistinctUntilChanged()
                .Where(x => MicrophoneLine != null)
                .Subscribe(x => { audioNotificationsManager.PlayNotification(x ? "beep300" : "beep750"); })
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.MicrophoneLine)
                .Subscribe(x =>
                {
                    microphoneController.LineId = x;
                    var config = configProvider.ActualConfig.CloneJson();
                    config.MicrophoneLineId = x;
                    configProvider.Save(config);
                })
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.IsPushToTalkMode)
                .Where(x => x)
                .Subscribe(() =>
                {
                    var anchors = new CompositeDisposable();
                    
                    eventSource.WhenMouseDown
                        .Where(x => x.Button == MouseButtons.XButton1)
                        .ObserveOnDispatcher()
                        .Subscribe(() => MicrophoneMuted = false, Log.HandleException)
                        .AddTo(anchors);
            
                    eventSource.WhenMouseUp
                        .Where(x => x.Button == MouseButtons.XButton1)
                        .ObserveOnDispatcher()
                        .Subscribe(() => MicrophoneMuted = true, Log.HandleException)
                        .AddTo(anchors);

                    buttonModeAnchors.Disposable = anchors;
                })
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.IsPushToTalkMode)
                .Where(x => !x)
                .Subscribe(() =>
                {
                    var anchors = new CompositeDisposable();

                    eventSource.WhenMouseDown
                        .Where(x => x.Button == MouseButtons.XButton1)
                        .ObserveOnDispatcher()
                        .Subscribe(() =>
                        {
                            var current = MicrophoneMuted;
                            MicrophoneMuted = !current;
                        }, Log.HandleException)
                        .AddTo(anchors);

                    buttonModeAnchors.Disposable = anchors;
                })
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.IsPushToTalkMode)
                .Subscribe(x =>
                {
                    var config = configProvider.ActualConfig.CloneJson();
                    config.IsPushToTalkMode = x;
                    configProvider.Save(config);
                })
                .AddTo(Anchors);
            
            Microphones = new ReadOnlyObservableCollection<MicrophoneLineData>(
                new ObservableCollection<MicrophoneLineData>(new MicrophoneProvider().EnumerateLines())
            );
            
            MicrophoneLine = Microphones
                .FirstOrDefault(x => configProvider.ActualConfig.MicrophoneLineId == null || x.LineId == configProvider.ActualConfig.MicrophoneLineId?.LineId);
            
            ToggleOverlayLockCommand = new DelegateCommand(() =>
            {
                if (overlay.IsLocked && overlay.UnlockWindowCommand.CanExecute(null))
                {
                    overlay.UnlockWindowCommand.Execute(null);
                } else if (!overlay.IsLocked && overlay.LockWindowCommand.CanExecute(null))
                {
                    overlay.LockWindowCommand.Execute(null);
                }
            });
            
            ExitAppCommand = new DelegateCommand(() =>
            {
                Log.Debug($"Closing application");
                configProvider.Save(configProvider.ActualConfig);
                Application.Current.Shutdown();
            });

            this.WhenAnyValue(x => x.WindowState)
                .Subscribe(x => ShowInTaskbar = x != WindowState.Minimized)
                .AddTo(Anchors);
            
            ShowAppCommand = new DelegateCommand(() => { WindowState = WindowState.Normal; });
            
            var executingAssembly = Assembly.GetExecutingAssembly();
            Title = $"{(AppArguments.Instance.IsDebugMode ? "[D]" : "")} {executingAssembly.GetName().Name} v{executingAssembly.GetName().Version}";
        }

        public ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }
        
        public ICommand ToggleOverlayLockCommand { get; }
        
        public ICommand ExitAppCommand { get; }
        
        public ICommand ShowAppCommand { get; }
        
        public IMicSwitchOverlayViewModel Overlay { get; }

        public WindowState WindowState
        {
            get => windowState;
            set => this.RaiseAndSetIfChanged(ref windowState, value);
        }

        public Visibility TrayIconVisibility
        {
            get => trayIconVisibility;
            set => this.RaiseAndSetIfChanged(ref trayIconVisibility, value);
        }

        public bool ShowInTaskbar
        {
            get => showInTaskbar;
            set => this.RaiseAndSetIfChanged(ref showInTaskbar, value);
        }
        
        public string Title { get; }

        public bool IsPushToTalkMode
        {
            get => isPushToTalkMode;
            set => this.RaiseAndSetIfChanged(ref isPushToTalkMode, value);
        }

        public MicrophoneLineData MicrophoneLine
        {
            get => microphoneLine;
            set => this.RaiseAndSetIfChanged(ref microphoneLine, value);
        }

        public double MicrophoneVolume
        {
            get => microphoneController.VolumePercent ?? 0;
            set => microphoneController.VolumePercent = value;
        }

        public bool MicrophoneMuted
        {
            get => microphoneController.Mute ?? false;
            set => microphoneController.Mute = value;
        }
    }
}