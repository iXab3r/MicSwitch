namespace MicSwitch.MainWindow.ViewModels;

internal interface IOutputControllerViewModel : IMediaController
{
    public IHotkeyEditorViewModel HotkeyToggleMute { get; }
    public IHotkeyEditorViewModel HotkeyMute { get; }
    public IHotkeyEditorViewModel HotkeyUnmute { get; }
    public IHotkeyEditorViewModel HotkeyVolumeUp { get; }
    public IHotkeyEditorViewModel HotkeyVolumeDown { get; }
}