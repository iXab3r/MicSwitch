using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
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
        private readonly IFactory<IHotkeyTracker> hotkeyTrackerFactory;
        private readonly IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory;
        private readonly IConfigProvider<MicSwitchHotkeyConfig> hotkeyConfigProvider;
        private readonly IScheduler uiScheduler;

        private MuteMode muteMode;
        private MicrophoneLineData microphoneLine;
        private bool microphoneVolumeControlEnabled;
        private bool enableAdvancedHotkeys;
        private MicrophoneState initialMicrophoneState;

        public MicrophoneControllerViewModel(
            IMicrophoneControllerEx microphoneController,
            IMicrophoneProvider microphoneProvider,
            IComplexHotkeyTracker hotkeyTracker,
            IFactory<IHotkeyTracker> hotkeyTrackerFactory,
            IFactory<IHotkeyEditorViewModel> hotkeyEditorFactory,
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
            this.hotkeyTrackerFactory = hotkeyTrackerFactory;
            this.hotkeyEditorFactory = hotkeyEditorFactory;
            this.hotkeyConfigProvider = hotkeyConfigProvider;
            this.uiScheduler = uiScheduler;
            MuteMicrophoneCommand = CommandWrapper.Create<object>(MuteMicrophoneCommandExecuted);
            Hotkey = PrepareHotkey(x => x.Hotkey, (config, hotkeyConfig) => config.Hotkey = hotkeyConfig);
            HotkeyToggle = PrepareHotkey(x => x.HotkeyForToggle, (config, hotkeyConfig) => config.HotkeyForToggle = hotkeyConfig);
            HotkeyMute = PrepareHotkey(x => x.HotkeyForMute, (config, hotkeyConfig) => config.HotkeyForMute = hotkeyConfig);
            HotkeyUnmute = PrepareHotkey(x => x.HotkeyForUnmute, (config, hotkeyConfig) => config.HotkeyForUnmute = hotkeyConfig);
            HotkeyPushToMute = PrepareHotkey(x => x.HotkeyForPushToMute, (config, hotkeyConfig) => config.HotkeyForPushToMute = hotkeyConfig);
            HotkeyPushToTalk = PrepareHotkey(x => x.HotkeyForPushToTalk, (config, hotkeyConfig) => config.HotkeyForPushToTalk = hotkeyConfig);

            PrepareTracker(HotkeyMode.Click, HotkeyToggle)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Toggling microphone state: {microphoneController}");
                    microphoneController.Mute = !microphoneController.Mute;
                }, Log.HandleUiException)
                .AddTo(Anchors);    
            
            PrepareTracker(HotkeyMode.Hold, HotkeyMute)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .Where(x => x.Value)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Muting microphone: {microphoneController}");
                    microphoneController.Mute = true;
                }, Log.HandleUiException)
                .AddTo(Anchors);   
            
            PrepareTracker(HotkeyMode.Hold, HotkeyUnmute)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .Where(x => x.Value)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Un-muting microphone: {microphoneController}");
                    microphoneController.Mute = false;
                }, Log.HandleUiException)
                .AddTo(Anchors);   
            
            PrepareTracker(HotkeyMode.Hold, HotkeyPushToTalk)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Processing push-to-talk hotkey for microphone: {microphoneController}");
                    microphoneController.Mute = !x.Value;
                }, Log.HandleUiException)
                .AddTo(Anchors);   
            
            PrepareTracker(HotkeyMode.Hold, HotkeyPushToMute)
                .ObservableForProperty(x => x.IsActive, skipInitial: true)
                .SubscribeSafe(x =>
                {
                    Log.Debug($"[{x.Sender}] Processing push-to-mute hotkey for microphone: {microphoneController}");
                    microphoneController.Mute = x.Value;
                }, Log.HandleUiException)
                .AddTo(Anchors);
            
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
                
            hotkeyConfigProvider.ListenTo(x => x.EnableAdvancedHotkeys)
                .ObserveOn(uiScheduler)
                .Subscribe(x => EnableAdditionalHotkeys = x)
                .AddTo(Anchors);
            
            hotkeyConfigProvider.ListenTo(x => x.InitialMicrophoneState)
                .ObserveOn(uiScheduler)
                .Subscribe(x => InitialMicrophoneState = x)
                .AddTo(Anchors);
            
            this.WhenAnyValue(x => x.MuteMode, x => x.InitialMicrophoneState)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(_ =>
                {
                    Log.Debug($"Processing muteMode: {muteMode}, {microphoneController}.Mute: {microphoneController.Mute}");
                    switch (muteMode)
                    {
                        case MuteMode.PushToTalk:
                            Log.Debug($"{muteMode} mute mode is enabled, un-muting microphone");
                            microphoneController.Mute = true;
                            break;
                        case MuteMode.PushToMute:
                            microphoneController.Mute = false;
                            Log.Debug($"{muteMode} mute mode is enabled, muting microphone");
                            break;
                        case MuteMode.ToggleMute when initialMicrophoneState == MicrophoneState.Mute:
                            Log.Debug($"{muteMode} enabled, muting microphone");
                            microphoneController.Mute = true;
                            break;
                        case MuteMode.ToggleMute when initialMicrophoneState == MicrophoneState.Unmute:
                            Log.Debug($"{muteMode} enabled, un-muting microphone");
                            microphoneController.Mute = false;
                            break;
                        default:
                            Log.Debug($"{muteMode} enabled, action is not needed");
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
            
             Observable.Merge(
                    this.ObservableForProperty(x => x.MuteMode, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.EnableAdditionalHotkeys, skipInitial: true).ToUnit(),
                    this.ObservableForProperty(x => x.InitialMicrophoneState, skipInitial: true).ToUnit(),
                    Hotkey.ObservableForProperty(x => x.Properties, skipInitial: true).ToUnit())
                .Throttle(ConfigThrottlingTimeout)
                .ObserveOn(uiScheduler)
                .SubscribeSafe(() =>
                {
                    var hotkeyConfig = hotkeyConfigProvider.ActualConfig.CloneJson();
                    hotkeyConfig.Hotkey = Hotkey.Properties;
                    hotkeyConfig.MuteMode = muteMode;
                    hotkeyConfig.EnableAdvancedHotkeys = enableAdvancedHotkeys;
                    hotkeyConfig.InitialMicrophoneState = initialMicrophoneState;
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
        
        public IHotkeyEditorViewModel HotkeyToggle { get; }
        
        public IHotkeyEditorViewModel HotkeyMute { get; }
        
        public IHotkeyEditorViewModel HotkeyUnmute { get; }
        
        public IHotkeyEditorViewModel HotkeyPushToTalk { get; }
        
        public IHotkeyEditorViewModel HotkeyPushToMute { get; }
        
        public bool MicrophoneMuted
        {
            get => microphoneController.Mute ?? false;
        }

        public bool EnableAdditionalHotkeys
        {
            get => enableAdvancedHotkeys;
            set => RaiseAndSetIfChanged(ref enableAdvancedHotkeys, value);
        }
        
        public MicrophoneLineData MicrophoneLine
        {
            get => microphoneLine;
            set => this.RaiseAndSetIfChanged(ref microphoneLine, value);
        }

        public MicrophoneState InitialMicrophoneState
        {
            get => initialMicrophoneState;
            set => RaiseAndSetIfChanged(ref initialMicrophoneState, value);
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

        private IHotkeyEditorViewModel PrepareHotkey(
            Expression<Func<MicSwitchHotkeyConfig, HotkeyConfig>> fieldToMonitor,
            Action<MicSwitchHotkeyConfig, HotkeyConfig> consumer)
        {
            return PrepareHotkey(
                this,
                fieldToMonitor,
                consumer).AddTo(Anchors);
        }

        private IHotkeyTracker PrepareTracker(
            HotkeyMode hotkeyMode,
            IHotkeyEditorViewModel hotkeyEditor)
        {
            return PrepareTracker(
                this,
                hotkeyMode,
                hotkeyEditor);
        }

        private async Task MuteMicrophoneCommandExecuted(object arg)
        {
            var mute = arg switch
            {
                bool argBool => argBool,
                _ => !microphoneController.Mute
            };
            Log.Debug($"{(mute == true ? "Muting" : "Un-muting")} microphone {microphoneController.LineId}");
            microphoneController.Mute = mute;
        }
        
        private static IHotkeyTracker PrepareTracker(
            MicrophoneControllerViewModel owner,
            HotkeyMode hotkeyMode, 
            IHotkeyEditorViewModel hotkeyEditor)
        {
            var result = owner.hotkeyTrackerFactory.Create();
            result.HotkeyMode = hotkeyMode;
            Observable.Merge(
                owner.WhenAnyValue(x => x.EnableAdditionalHotkeys).ToUnit(),
                hotkeyEditor.WhenAnyValue(x => x.Key, x => x.AlternativeKey, x => x.SuppressKey).ToUnit())
                .SubscribeSafe(x =>
                {
                    result.SuppressKey = hotkeyEditor.SuppressKey;
                    result.Clear();
                    if (!owner.EnableAdditionalHotkeys)
                    {
                        return;
                    }
                    
                    if (hotkeyEditor.Key != null)
                    {
                        result.Add(hotkeyEditor.Key);
                    }
                    if (hotkeyEditor.AlternativeKey != null)
                    {
                        result.Add(hotkeyEditor.AlternativeKey);
                    }

                    if (result.Hotkeys.Any())
                    {
                        owner.EnableAdditionalHotkeys = true;
                    }
                }, Log.HandleUiException)
                .AddTo(owner.Anchors);
            return result.AddTo(owner.Anchors);
        }

        private static IHotkeyEditorViewModel PrepareHotkey(
            MicrophoneControllerViewModel owner,
            Expression<Func<MicSwitchHotkeyConfig, HotkeyConfig>> fieldToMonitor,
            Action<MicSwitchHotkeyConfig, HotkeyConfig> consumer)
        {
            var result = owner.hotkeyEditorFactory.Create();
            
            owner.hotkeyConfigProvider.ListenTo(fieldToMonitor)
                .Select(x => x ?? HotkeyConfig.Empty)
                .ObserveOn(owner.uiScheduler)
                .SubscribeSafe(config =>
                {
                    Log.Debug($"Setting new hotkeys configuration: {config.DumpToTextRaw()}, current: {result.Properties}");
                    result.Load(config);
                }, Log.HandleException)
                .AddTo(owner.Anchors);
            
            result.ObservableForProperty(x => x.Properties, skipInitial: true)
                .Throttle(ConfigThrottlingTimeout)
                .SubscribeSafe(() =>
                {
                    var hotkeyConfig = owner.hotkeyConfigProvider.ActualConfig.CloneJson();
                    consumer(hotkeyConfig, result.Properties);
                    owner.hotkeyConfigProvider.Save(hotkeyConfig);
                }, Log.HandleUiException)
                .AddTo(owner.Anchors);
            
            return result;
        }
    }
}