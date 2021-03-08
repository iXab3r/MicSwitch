using System;
using System.ComponentModel;
using System.Windows;
using log4net;
using PoeShared.Modularity;
using PoeShared.Scaffolding;

namespace MicSwitch.MainWindow.Views
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow 
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindow));

        public MainWindow(IAppArguments appArguments)
        {
            using var sw = new BenchmarkTimer("MainWindow", Log);
            Log.Debug($"Initializing MainWindow for process {appArguments.ProcessId}");
            InitializeComponent();
            sw.Step($"BAML loaded");
        }
        
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"MainWindow unloaded");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"MainWindow loaded");
        }

        private void OnClosed(object sender, EventArgs e)
        {
            Log.Debug($"MainWindow closed");
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            Log.Debug($"MainWindow is closing");
        }
    }
}