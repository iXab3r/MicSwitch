using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using log4net;
using MicSwitch.MainWindow.Models;
using MicSwitch.Modularity;
using MicSwitch.Services;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;
using PoeShared.UI.Hotkeys;
using ReactiveUI;
using Unity;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class MicrophoneControllerViewModel : DisposableReactiveObject, IMicrophoneControllerViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MicrophoneControllerViewModel));
        private static readonly TimeSpan ConfigThrottlingTimeout = TimeSpan.FromMilliseconds(250);

        private readonly IMicrophoneControllerEx microphoneController;

        private MuteMode muteMode;
        private MicrophoneLineData microphoneLine;
        private bool microphoneVolumeControlEnabled;

        public MicrophoneControllerViewModel(
            IMicrophoneControllerEx microphoneController,
            IMicrophoneProvider microphoneProvider,
            IComplexHotkeyTracker hotkeyTracker,
            IHotkeyConverter hotkeyConverter,
            IFactory<IHotkeyTracker> hotkeyTrackerFactory,
            IFactory<HotkeyEditorViewModel> hotkeyEditorFactory,
            IConfigProvider<MicSwitchConfig> configProvider,
            IConfigProvider<MicSwitchHotkeyConfig> hotkeyConfigProvider,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            microphoneProvider.Microphones
                .ToObservableChangeSet()
                .ObserveOn(uiScheduler)
                .Bind(out var microphones)
                .SubscribeToErrors(Log.HandleUiException)
                .AddTo(Anchors);
            Microphones = microphones;
            
            this.microphoneController = microphoneController;
            MuteMicrophoneCommand = CommandWrapper.Create<bool>(MuteMicrophoneCommandExecuted);
            Hotkey = hotkeyEditorFactory.Create();

            this.RaiseWhenSourceValue(x => x.MicrophoneVolume, microphoneController, x => x.VolumePercent, uiScheduler).AddTo(Anchors);
            this.RaiseWhenSourceValue(x => x.MicrophoneMuted, microphoneController, x => x.Mute, uiScheduler).AddTo(Anchors);
            
            this.WhenAnyValue(x => x.MicrophoneLine)
                .DistinctUntilChanged()
                .SubscribeSafe(x => microphoneController.LineId = x, Log.HandleUiException)
                .AddTo(Anchors);
            
            hotkeyConfigProvider.ListenTo(x => x.MuteMode)
                .ObserveOn(uiScheduler)
                .Subscribe(x =>
                {
                    Log.Debug($"Mute mode loaded from config: {x}");
                    MuteMode = x;
                })
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.MuteMode)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(newMuteMode =>
                {
                    switch (newMuteMode)
                    {
                        case MuteMode.PushToTalk:
                            Log.Debug($"{newMuteMode} mute mode is enabled, un-muting microphone");
                            microphoneController.Mute = true;
                            break;
                        case MuteMode.PushToMute:
                            microphoneController.Mute = false;
                            Log.Debug($"{newMuteMode} mute mode is enabled, muting microphone");
                            break;
                        default:
                            Log.Debug($"{newMuteMode} enabled, mic action is not needed");
                            break;
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);  
            
            hotkeyTracker
                .WhenAnyValue(x => x.IsActive)
                .Skip(1)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(async isActive =>
                {
                    Log.Debug($"Handling hotkey press (isActive: {isActive}), mute mode: {muteMode}");
                    switch (muteMode)
                    {
                        case MuteMode.PushToTalk:
                            microphoneController.Mute = !isActive;
                            break;
                        case MuteMode.PushToMute:
                            microphoneController.Mute = isActive;
                            break;
                        case MuteMode.ToggleMute:
                            microphoneController.Mute = !microphoneController.Mute;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(muteMode), muteMode, @"Unsupported mute mode");
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
            hotkeyConfigProvider.ListenTo(x => x.Hotkey)
                .Select(x => x ?? HotkeyConfig.Empty)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(config =>
                {
                    Log.Debug($"Setting new hotkeys configuration: {config.DumpToTextRaw()}, current: {Hotkey.Properties}");
                    Hotkey.Load(config);
                }, Log.HandleException)
                .AddTo(Anchors);
            
             Observable.Merge(
                    this.ObservableForProperty(x => x.MuteMode, skipInitial: true).ToUnit(),
                    Hotkey.ObservableForProperty(x => x.Properties, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() =>
                {
                    var hotkeyConfig = hotkeyConfigProvider.ActualConfig.CloneJson();
                    hotkeyConfig.Hotkey = Hotkey.Properties;
                    hotkeyConfig.MuteMode = muteMode;
                    hotkeyConfigProvider.Save(hotkeyConfig);
                }, Log.HandleUiException)
                .AddTo(Anchors);
             
             Observable.Merge(
                     this.ObservableForProperty(x => x.MicrophoneLine, skipInitial: true).ToUnit(),
                     this.ObservableForProperty(x => x.MicrophoneVolumeControlEnabled, skipInitial: true).ToUnit())
                 .Throttle(ConfigThrottlingTimeout)
                 .ObserveOn(uiScheduler)
                 .SubscribeSafe(() =>
                 {
                     var config = configProvider.ActualConfig.CloneJson();
                     config.MicrophoneLineId = microphoneLine;
                     config.VolumeControlEnabled = microphoneVolumeControlEnabled;
                     configProvider.Save(config);
                 }, Log.HandleUiException)
                 .AddTo(Anchors);
            
             configProvider.ListenTo(x => x.VolumeControlEnabled)
                 .ObserveOn(uiScheduler)
                 .SubscribeSafe(x => MicrophoneVolumeControlEnabled = x, Log.HandleException)
                 .AddTo(Anchors);
            
             Observable.Merge(
                     configProvider.ListenTo(x => x.MicrophoneLineId).ToUnit(),
                     Microphones.ToObservableChangeSet().ToUnit())
                 .Select(_ => configProvider.ActualConfig.MicrophoneLineId)
                 .ObserveOn(uiScheduler)
                 .SubscribeSafe(configLineId =>
                 {
                     Log.Debug($"Microphone line configuration changed, lineId: {configLineId}, known lines: {Microphones.DumpToTextRaw()}");

                     var micLine = Microphones.FirstOrDefault(line => line.Equals(configLineId));
                     if (micLine.IsEmpty)
                     {
                         Log.Debug($"Selecting first one of available microphone lines, known lines: {Microphones.DumpToTextRaw()}");
                         micLine = Microphones.FirstOrDefault();
                     }
                     MicrophoneLine = micLine;
                     MuteMicrophoneCommand.ResetError();
                 }, Log.HandleUiException)
                 .AddTo(Anchors);
        }
        
        public ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }
        
        public MuteMode MuteMode
        {
            get => muteMode;
            set => RaiseAndSetIfChanged(ref muteMode, value);
        }
        
        public IHotkeyEditorViewModel Hotkey { get; }
        
        public bool MicrophoneMuted
        {
            get => microphoneController.Mute ?? false;
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

        public bool MicrophoneVolumeControlEnabled
        {
            get => microphoneVolumeControlEnabled;
            set => RaiseAndSetIfChanged(ref microphoneVolumeControlEnabled, value);
        }
        
        public CommandWrapper MuteMicrophoneCommand { get; }

        private async Task MuteMicrophoneCommandExecuted(bool mute)
        {
            Log.Debug($"{(mute ? "Muting" : "Un-muting")} microphone {microphoneController.LineId}");
            microphoneController.Mute = mute;
        }
    }
}