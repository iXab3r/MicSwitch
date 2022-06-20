using MicSwitch.MainWindow.Models;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicrophoneControllerViewModel : IDisposableReactiveObject
    {
        ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }
        
        MuteMode MuteMode { get; set; }
        
        bool EnableAdditionalHotkeys { get; set; }
        
        IHotkeyEditorViewModel Hotkey { get; }
        
        IHotkeyEditorViewModel HotkeyToggle { get; }
        
        IHotkeyEditorViewModel HotkeyMute { get; }
        
        IHotkeyEditorViewModel HotkeyUnmute { get; }
        
        IHotkeyEditorViewModel HotkeyPushToTalk { get; }
        
        IHotkeyEditorViewModel HotkeyPushToMute { get; }
        
        bool MicrophoneMuted { get; }
        
        MicrophoneLineData MicrophoneLine { get; set; }
        
        double MicrophoneVolume { get; set; }
        
        bool MicrophoneVolumeControlEnabled { get; set; }
        
        MicrophoneState InitialMicrophoneState { get; set; }
        
        CommandWrapper MuteMicrophoneCommand { get; }
    }
}