using System;
using System.Drawing;
using System.IO;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Interactivity;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using PoeShared.Scaffolding;

namespace MicSwitch.MainWindow.Models
{
    internal class TaskBarBitmapImageBehavior : Behavior<TaskbarIcon>
    {
        public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
            "Image", typeof(BitmapSource), typeof(TaskBarBitmapImageBehavior), new PropertyMetadata(default(BitmapSource)));

        private readonly SerialDisposable attachmentAnchors = new SerialDisposable();

        public BitmapSource Image
        {
            get => (BitmapSource) GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            attachmentAnchors.Disposable =
                this.Observe(ImageProperty)
                    .Select(() => Image)
                    .Subscribe(HandleImageChange);
        }

        private void HandleImageChange(BitmapSource source)
        {
            if (source == null)
            {
                AssociatedObject.Icon = null;
            }
            else
            {
                var bitmap = source.ToBitmap();
                AssociatedObject.Icon = Icon.FromHandle(bitmap.GetHicon());
            }
        }

        protected override void OnDetaching()
        {
            attachmentAnchors.Disposable = null;
            base.OnDetaching();
        }

        
    }
}