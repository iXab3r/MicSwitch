using System.Windows.Input;
using MicSwitch.MainWindow.Models;

namespace MicSwitch.MainWindow.ViewModels
{
    internal sealed class HotkeyEditorViewModel : DisposableReactiveObjectWithLogger, IHotkeyEditorViewModel
    {
        private static readonly Binder<HotkeyEditorViewModel> Binder = new();

        private readonly IHotkeyConverter hotkeyConverter;
       
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

        public bool HasModifiers { get; [UsedImplicitly] private set; }

        public bool IsMouse { get; [UsedImplicitly] private set; }

        public HotkeyConfig Properties { get; private set; }

        public HotkeyGesture Key { get; set; }
        
        public string Description { get; set; }
        
        public HotkeyGesture AlternativeKey { get; set; }

        public bool SuppressKey { get; set; }
        
        public bool IgnoreModifiers { get; set; }

        private HotkeyConfig SaveToHotkeyConfig()
        {
            return new()
            {
                Key = hotkeyConverter.ConvertToString(Key ?? HotkeyGesture.Empty),
                AlternativeKey = hotkeyConverter.ConvertToString(AlternativeKey ?? HotkeyGesture.Empty),
                Suppress = SuppressKey,
                IgnoreModifiers = IgnoreModifiers
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