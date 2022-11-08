using MicSwitch.MainWindow.Models;
using PoeShared.Audio.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicrophoneControllerViewModel : IDisposableReactiveObject
    {
        ReadOnlyObservableCollection<MMDeviceLineData> Microphones { get; }
        
        MuteMode MuteMode { get; set; }
        
        bool EnableAdditionalHotkeys { get; set; }
        
        bool EnableOutputVolumeControl { get; set; }
        
        IHotkeyEditorViewModel Hotkey { get; }
        
        IHotkeyEditorViewModel HotkeyToggle { get; }
        
        IHotkeyEditorViewModel HotkeyMute { get; }
        
        IHotkeyEditorViewModel HotkeyUnmute { get; }
        
        IHotkeyEditorViewModel HotkeyPushToTalk { get; }
        
        IHotkeyEditorViewModel HotkeyOutputMute { get; }
        
        IHotkeyEditorViewModel HotkeyOutputVolumeUp { get; }
        
        IHotkeyEditorViewModel HotkeyOutputVolumeDown { get; }
        
        IHotkeyEditorViewModel HotkeyPushToMute { get; }
        
        IWaveOutDeviceSelectorViewModel OutputDeviceSelector { get; }
        
        double OutputDeviceVolume { get; set; }
        
        bool MicrophoneMuted { get; }
        
        MMDeviceLineData MMDeviceLine { get; set; }
        
        double MicrophoneVolume { get; set; }
        
        bool MicrophoneVolumeControlEnabled { get; set; }
        
        MicrophoneState InitialMicrophoneState { get; set; }
        
        CommandWrapper MuteMicrophoneCommand { get; }
    }
}