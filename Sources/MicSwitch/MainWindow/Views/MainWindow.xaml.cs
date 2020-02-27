using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows;
using log4net;
using MicSwitch.MainWindow.Models;
using MicSwitch.MainWindow.ViewModels;
using MicSwitch.Prism;
using MicSwitch.Services;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Native;
using PoeShared.Native.Scaffolding;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Squirrel.Prism;
using PoeShared.Squirrel.Updater;
using PoeShared.Wpf.Scaffolding;
using Unity;
using Unity.Resolution;

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
    }
}