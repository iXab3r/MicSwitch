using System.Drawing;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Interactivity;
using Hardcodet.Wpf.TaskbarNotification;
using log4net;
using PoeShared;
using PoeShared.Scaffolding;

namespace MicSwitch.MainWindow.Models
{
    internal class TaskBarBitmapImageBehavior : Behavior<TaskbarIcon>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TaskBarBitmapImageBehavior));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", typeof(Icon), typeof(TaskBarBitmapImageBehavior), new PropertyMetadata(default(Icon)));

        private readonly SerialDisposable attachmentAnchors = new SerialDisposable();

        public Icon Icon
        {
            get => (Icon) GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            attachmentAnchors.Disposable =
                this.Observe(IconProperty)
                    .Select(_ => Icon)
                    .SubscribeSafe(HandleImageChange, Log.HandleUiException);
        }

        private void HandleImageChange(Icon source)
        {
            AssociatedObject.Icon = source;
        }

        protected override void OnDetaching()
        {
            attachmentAnchors.Disposable = null;
            base.OnDetaching();
        }

        
    }
}