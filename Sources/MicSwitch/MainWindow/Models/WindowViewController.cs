using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows;
using log4net;
using PoeShared.Native;

namespace MicSwitch.MainWindow.Models
{
    internal sealed class WindowViewController : IViewController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WindowViewController));

        private readonly Window owner;
        private readonly ISubject<Unit> whenLoaded = new Subject<Unit>();

        public WindowViewController(Window owner)
        {
            this.owner = owner;
            owner.Loaded += OnLoaded;
            owner.Unloaded += OnUnloaded;
        }

        public IObservable<Unit> WhenLoaded => whenLoaded;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"[{owner}.{owner.Title}] Window unloaded");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"[{owner}.{owner.Title}] Window loaded");
            whenLoaded.OnNext(Unit.Default);
        }
    }
}