using System.Drawing;
using System.Windows.Media;
using PoeShared.Scaffolding;

namespace MicSwitch.MainWindow.Models
{
    internal interface IImageProvider : IDisposableReactiveObject
    {
        ImageSource MicrophoneImage { get; }
        Icon MicrophoneImageAsIcon { get; }

        ImageSource StreamingMicrophoneImage { get; }
        ImageSource MutedMicrophoneImage { get; }
        
    }
}