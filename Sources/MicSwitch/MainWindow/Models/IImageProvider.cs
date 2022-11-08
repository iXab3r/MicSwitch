using System.ComponentModel;
using System.Drawing;
using System.Windows.Media;
using MicSwitch.Services;

namespace MicSwitch.MainWindow.Models
{
    internal interface IImageProvider : INotifyPropertyChanged
    {
        ImageSource MicrophoneImage { get; }
        Icon MicrophoneImageAsIcon { get; }

        ImageSource StreamingMicrophoneImage { get; }
        ImageSource MutedMicrophoneImage { get; }
        
        IMMDeviceController MicrophoneDeviceController { get; set; }
    }
}