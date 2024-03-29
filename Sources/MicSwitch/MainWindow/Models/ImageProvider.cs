using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MicSwitch.Modularity;
using MicSwitch.Services;

namespace MicSwitch.MainWindow.Models
{
    internal sealed class ImageProvider : DisposableReactiveObjectWithLogger, IImageProvider
    {
        private readonly BitmapImage defaultMicrophoneImage = new BitmapImage(new Uri("pack://application:,,,/Resources/microphoneEnabled.ico", UriKind.RelativeOrAbsolute));
        private readonly BitmapImage defaultMutedMicrophoneImage = new BitmapImage(new Uri("pack://application:,,,/Resources/microphoneDisabled.ico", UriKind.RelativeOrAbsolute));

        public ImageProvider(
            [NotNull] IConfigProvider<MicSwitchOverlayConfig> configProvider,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            Observable.Merge(
                    this.WhenAnyValue(x => x.MicrophoneDeviceController.Mute).ToUnit(),
                    this.WhenAnyValue(x => x.StreamingMicrophoneImage).ToUnit(),
                    this.WhenAnyValue(x => x.MutedMicrophoneImage).ToUnit())
                .ObserveOn(uiScheduler)
                .Select(x => MicrophoneDeviceController?.Mute ?? false ? MutedMicrophoneImage : StreamingMicrophoneImage)
                .SubscribeSafe(x => MicrophoneImage = x, Log.HandleUiException)
                .AddTo(Anchors);

            this.WhenAnyValue(x => x.MicrophoneImage)
                .Select(x => x as BitmapSource)
                .SelectSafeOrDefault(x => x == null ? default : Icon.FromHandle(x.ToBitmap().GetHicon()))
                .DisposePrevious()
                .SubscribeSafe(x => MicrophoneImageAsIcon = x, Log.HandleUiException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.MicrophoneIcon)
                .ObserveOn(uiScheduler)
                .SelectSafeOrDefault(x => x.ToBitmapImage())
                .SubscribeSafe(x => StreamingMicrophoneImage = x ?? defaultMicrophoneImage, Log.HandleUiException)
                .AddTo(Anchors);
            
            configProvider.ListenTo(x => x.MutedMicrophoneIcon)
                .ObserveOn(uiScheduler)
                .SelectSafeOrDefault(x => x.ToBitmapImage())
                .SubscribeSafe(x => MutedMicrophoneImage = x ?? defaultMutedMicrophoneImage, Log.HandleUiException)
                .AddTo(Anchors);
        }

        public ImageSource MicrophoneImage { get; private set; }

        public ImageSource StreamingMicrophoneImage { get; private set; }

        public ImageSource MutedMicrophoneImage { get; private set; }

        public Icon MicrophoneImageAsIcon { get; private set; }
        
        public IMMDeviceController MicrophoneDeviceController { get; set; }
    }
}