using System.Windows.Media;
using PoeShared.Scaffolding;

namespace MicSwitch.MainWindow.Models
{
    internal interface IImageProvider : IDisposableReactiveObject
    {
        ImageSource ActiveMicrophoneImage { get; }
        ImageSource MicrophoneImage { get; }
        ImageSource MutedMicrophoneImage { get; }
    }
}