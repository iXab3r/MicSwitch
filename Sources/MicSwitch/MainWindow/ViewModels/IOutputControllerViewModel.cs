namespace MicSwitch.MainWindow.ViewModels;

internal interface IOutputControllerViewModel : IMediaController
{
    IHotkeyEditorViewModel HotkeyOutputMute { get; }
    IHotkeyEditorViewModel HotkeyOutputVolumeUp { get; }
    IHotkeyEditorViewModel HotkeyOutputVolumeDown { get; }
}