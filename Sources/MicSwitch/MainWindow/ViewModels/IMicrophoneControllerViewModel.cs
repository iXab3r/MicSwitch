using MicSwitch.MainWindow.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicrophoneControllerViewModel : IMediaController
    {
        MuteMode MuteMode { get; set; }
        
        IHotkeyEditorViewModel Hotkey { get; }
        
        IHotkeyEditorViewModel HotkeyToggle { get; }
        
        IHotkeyEditorViewModel HotkeyMute { get; }
        
        IHotkeyEditorViewModel HotkeyUnmute { get; }
        
        IHotkeyEditorViewModel HotkeyPushToTalk { get; }
        
        IHotkeyEditorViewModel HotkeyPushToMute { get; }
        
        bool MicrophoneVolumeControlEnabled { get; set; }
        
        MicrophoneState InitialMicrophoneState { get; set; }
    }
}