using System.Windows.Input;
using log4net;
using MicSwitch.MainWindow.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class HotkeyEditorViewModel : DisposableReactiveObject, IHotkeyEditorViewModel
    {
        private static readonly Binder<HotkeyEditorViewModel> Binder = new();
        private static readonly ILog Log = LogManager.GetLogger(typeof(HotkeyEditorViewModel));

        private readonly IHotkeyConverter hotkeyConverter;
        private HotkeyGesture alternativeKey;
        private HotkeyGesture key;
        private bool suppressKey;
        private HotkeyConfig properties;
        private bool ignoreModifiers;
        private bool hasModifiers;
        private bool isMouse;
        private string description;

        static HotkeyEditorViewModel()
        {
            Binder
                .Bind(x => x.Key != null && x.Key.ModifierKeys != ModifierKeys.None || x.AlternativeKey != null && x.AlternativeKey.ModifierKeys != ModifierKeys.None)
                .To(x => x.HasModifiers);
            Binder.BindIf(x => x.HasModifiers, x => false).To(x => x.IgnoreModifiers);
        }

        public HotkeyEditorViewModel(IHotkeyConverter hotkeyConverter)
        {
            this.hotkeyConverter = hotkeyConverter;
            this.WhenAnyValue(x => x.Key, x => x.AlternativeKey, x => x.SuppressKey, x => x.IgnoreModifiers)
                .Select(x => SaveToHotkeyConfig())
                .SubscribeSafe(x => Properties = x, Log.HandleUiException)
                .AddTo(Anchors);
            
            Binder.Attach(this).AddTo(Anchors);
        }

        public bool HasModifiers
        {
            get => hasModifiers;
            private set => RaiseAndSetIfChanged(ref hasModifiers, value);
        }

        public bool IsMouse
        {
            get => isMouse;
            private set => RaiseAndSetIfChanged(ref isMouse, value);
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

        
        public string Description
        {
            get => description;
            set => RaiseAndSetIfChanged(ref description, value);
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
        
        public bool IgnoreModifiers
        {
            get => ignoreModifiers;
            set => RaiseAndSetIfChanged(ref ignoreModifiers, value);
        }

        private HotkeyConfig SaveToHotkeyConfig()
        {
            return new()
            {
                Key = hotkeyConverter.ConvertToString(key ?? HotkeyGesture.Empty),
                AlternativeKey = hotkeyConverter.ConvertToString(alternativeKey ?? HotkeyGesture.Empty),
                Suppress = suppressKey,
                IgnoreModifiers = ignoreModifiers
            };
        }

        public void Load(HotkeyConfig config)
        {
            try
            {
                Key = hotkeyConverter.ConvertFromString(config.Key ?? string.Empty);
                AlternativeKey = hotkeyConverter.ConvertFromString(config.AlternativeKey ?? string.Empty);
                SuppressKey = config.Suppress;
                IgnoreModifiers = config.IgnoreModifiers;
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