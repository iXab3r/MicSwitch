using System;
using System.Reactive.Linq;
using log4net;
using MicSwitch.MainWindow.Models;
using PoeShared;
using PoeShared.Scaffolding;
using PoeShared.UI;
using ReactiveUI;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class HotkeyEditorViewModel : DisposableReactiveObject, IHotkeyEditorViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HotkeyEditorViewModel));

        private readonly IHotkeyConverter hotkeyConverter;
        private HotkeyGesture alternativeKey;
        private HotkeyGesture key;
        private bool suppressKey;
        private HotkeyConfig properties;

        public HotkeyEditorViewModel(IHotkeyConverter hotkeyConverter)
        {
            this.hotkeyConverter = hotkeyConverter;
            this.WhenAnyValue(x => x.Key, x => x.AlternativeKey, x => x.SuppressKey)
                .Select(x => SaveToHotkeyConfig())
                .SubscribeSafe(x => Properties = x, Log.HandleUiException)
                .AddTo(Anchors);
        }

        public HotkeyConfig Properties
        {
            get => properties;
            private set => RaiseAndSetIfChanged(ref properties, value);
        }

        public HotkeyGesture Key
        {
            get => key;
            set => RaiseAndSetIfChanged(ref key, value);
        }

        public HotkeyGesture AlternativeKey
        {
            get => alternativeKey;
            set => RaiseAndSetIfChanged(ref alternativeKey, value);
        }

        public bool SuppressKey
        {
            get => suppressKey;
            set => RaiseAndSetIfChanged(ref suppressKey, value);
        }

        private HotkeyConfig SaveToHotkeyConfig()
        {
            return new()
            {
                Key = hotkeyConverter.ConvertToString(key ?? HotkeyGesture.Empty),
                AlternativeKey = hotkeyConverter.ConvertToString(alternativeKey ?? HotkeyGesture.Empty),
                Suppress = suppressKey
            };
        }

        public void Load(HotkeyConfig config)
        {
            try
            {
                Key = hotkeyConverter.ConvertFromString(config.Key ?? string.Empty);
                AlternativeKey = hotkeyConverter.ConvertFromString(config.AlternativeKey ?? string.Empty);
                SuppressKey = config.Suppress;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to parse config hotkeys: {config}", e);
                Key = HotkeyGesture.Empty;
                AlternativeKey = HotkeyGesture.Empty;
                SuppressKey = true;
            }
        }
    }
}