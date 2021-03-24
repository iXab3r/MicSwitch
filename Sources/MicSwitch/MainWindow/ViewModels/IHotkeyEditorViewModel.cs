using MicSwitch.MainWindow.Models;
using PoeShared.Scaffolding;
using PoeShared.UI.Hotkeys;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IHotkeyEditorViewModel : IDisposableReactiveObject
    {
        HotkeyConfig Properties { get; }
        
        HotkeyGesture Key { get; set; }
        
        HotkeyGesture AlternativeKey { get; set; }
        
        bool SuppressKey { get; set; }

        void Load(HotkeyConfig properties);
    }
}