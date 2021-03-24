using System.Collections.ObjectModel;
using MicSwitch.MainWindow.Models;
using MicSwitch.Services;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IMicrophoneControllerViewModel : IDisposableReactiveObject
    {
        ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }
        
        MuteMode MuteMode { get; set; }
        
        IHotkeyEditorViewModel Hotkey { get; }
        
        bool MicrophoneMuted { get; }
        
        MicrophoneLineData MicrophoneLine { get; set; }
        
        double MicrophoneVolume { get; set; }
        
        bool MicrophoneVolumeControlEnabled { get; set; }
        
        CommandWrapper MuteMicrophoneCommand { get; }
    }
}