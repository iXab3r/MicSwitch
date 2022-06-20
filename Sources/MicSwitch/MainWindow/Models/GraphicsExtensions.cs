using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MicSwitch.MainWindow.Models
{
    internal static class GraphicsExtensions
    {
        public static byte[] ToBytes(this Icon icon)
        {
            if (icon == null)
            {
                return null;
            }
            using MemoryStream ms = new MemoryStream();
            icon.Save(ms);
            return ms.ToArray();
        }

        public static Icon ToIcon(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }
            using MemoryStream ms = new MemoryStream(bytes);
            return new Icon(ms);
        }
        
        public static Bitmap ToBitmap(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }
            using MemoryStream ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        
        public static BitmapSource ToImageSource(this Bitmap icon)
        {
            if (icon == null)
            {
                return null;
            }

            BitmapSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.GetHicon(),
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }
        
        public static BitmapImage ToBitmapImage(this byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }
            var result = new BitmapImage();
            result.BeginInit();
            //FIXME Should probably be disposed 
            result.StreamSource = new MemoryStream(data);
            result.EndInit();
            return result;
        }
        
        public static BitmapSource ToImageSource(this Icon icon)
        {
            if (icon == null)
            {
                return null;
            }

            BitmapSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        public static byte[] ToBytes(this Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return null;
            }
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);
            return memoryStream.ToArray();
        }
        
        public static Bitmap ToBitmap(this BitmapSource bitmapImage)
        {
            if (bitmapImage == null)
            {
                return null;
            }
            
            using var outStream = new MemoryStream();
            BitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmapImage));
            enc.Save(outStream);
            var bitmap = new Bitmap(outStream);
            return new Bitmap(bitmap);
        }
    }
}