using AutoCompleteTextBox.Editors;
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
        
        MicrophoneState InitialMicrophoneState { get; set; }
        
        IComboSuggestionProvider KnownDevices { get; set; }
    }
}