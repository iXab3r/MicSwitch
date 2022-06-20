using MicSwitch.MainWindow.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal interface IHotkeyEditorViewModel : IDisposableReactiveObject
    {
        HotkeyConfig Properties { get; }
        
        string Description { get; set; }
        
        HotkeyGesture Key { get; set; }
        
        HotkeyGesture AlternativeKey { get; set; }
        
        bool SuppressKey { get; set; }
        
        bool IgnoreModifiers { get; set; }

        void Load(HotkeyConfig properties);
    }
}