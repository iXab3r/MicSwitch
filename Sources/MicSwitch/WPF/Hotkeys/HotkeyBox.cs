using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PoeShared.Native;

namespace MicSwitch.WPF.Hotkeys
{
    [TemplatePart(Name = PART_TextBox, Type = typeof(TextBox))]
    public class HotKeyBox : Control
    {
        private const string PART_TextBox = "PART_TextBox";

        public static readonly DependencyProperty HotKeyProperty = DependencyProperty.Register(
            "HotKey", typeof(HotkeyGesture), typeof(HotKeyBox),
            new FrameworkPropertyMetadata(default(HotkeyGesture), OnHotKeyChanged) {BindsTwoWayByDefault = true});

        public static readonly DependencyProperty AreModifierKeysRequiredProperty = DependencyProperty.Register(
            "AreModifierKeysRequired", typeof(bool), typeof(HotKeyBox), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(
            "Watermark", typeof(string), typeof(HotKeyBox), new PropertyMetadata(default(string)));

        private static readonly DependencyPropertyKey TextPropertyKey = DependencyProperty.RegisterReadOnly(
            "Text", typeof(string), typeof(HotKeyBox), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty TextProperty = TextPropertyKey.DependencyProperty;

        private TextBox textBox;

        static HotKeyBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HotKeyBox), new FrameworkPropertyMetadata(typeof(HotKeyBox)));
            EventManager.RegisterClassHandler(typeof(HotKeyBox), GotFocusEvent, new RoutedEventHandler(OnGotFocus));
        }

        public HotkeyGesture HotKey
        {
            get => (HotkeyGesture) GetValue(HotKeyProperty);
            set => SetValue(HotKeyProperty, value);
        }

        public bool AreModifierKeysRequired
        {
            get => (bool) GetValue(AreModifierKeysRequiredProperty);
            set => SetValue(AreModifierKeysRequiredProperty, value);
        }

        public string Watermark
        {
            get => (string) GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public string Text
        {
            get => (string) GetValue(TextProperty);
            private set => SetValue(TextPropertyKey, value);
        }

        private static void OnHotKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HotKeyBox) d;
            ctrl.UpdateText();
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            var hotKeyBox = (HotKeyBox) sender;

            // If we're an editable HotKeyBox, forward focus to the TextBox or previous element
            if (e.Handled)
            {
                return;
            }

            if (!hotKeyBox.Focusable || hotKeyBox.textBox == null)
            {
                return;
            }

            if (!Equals(e.OriginalSource, hotKeyBox))
            {
                return;
            }

            // MoveFocus takes a TraversalRequest as its argument.
            var request = new TraversalRequest((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                ? FocusNavigationDirection.Previous
                : FocusNavigationDirection.Next);
            // Gets the element with keyboard focus.
            var elementWithFocus = Keyboard.FocusedElement as UIElement;
            // Change keyboard focus.
            elementWithFocus?.MoveFocus(request);
            e.Handled = true;
        }

        public override void OnApplyTemplate()
        {
            if (textBox != null)
            {
                textBox.PreviewMouseDown -= TextBoxOnPreviewMouseDown;
                textBox.PreviewKeyDown -= TextBoxOnPreviewKeyDown2;
                textBox.GotFocus -= TextBoxOnGotFocus;
                textBox.LostFocus -= TextBoxOnLostFocus;
                textBox.TextChanged -= TextBoxOnTextChanged;
            }

            base.OnApplyTemplate();

            textBox = Template.FindName(PART_TextBox, this) as TextBox;
            if (textBox == null)
            {
                return;
            }

            textBox.PreviewKeyDown += TextBoxOnPreviewKeyDown2;
            textBox.PreviewMouseDown += TextBoxOnPreviewMouseDown;

            textBox.GotFocus += TextBoxOnGotFocus;
            textBox.LostFocus += TextBoxOnLostFocus;
            textBox.TextChanged += TextBoxOnTextChanged;
            UpdateText();
        }

        private void TextBoxOnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var currentModifierKeys = GetCurrentModifierKeys();
            if (e.XButton1 == MouseButtonState.Pressed)
            {
                HotKey = new HotkeyGesture(MouseButton.XButton1, currentModifierKeys);
            }
            else if (e.XButton2 == MouseButtonState.Pressed)
            {
                HotKey = new HotkeyGesture(MouseButton.XButton2, currentModifierKeys);
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                HotKey = new HotkeyGesture(MouseButton.Middle, currentModifierKeys);
            }
            else
            {
                HotKey = null;
            }
        }

        private void TextBoxOnTextChanged(object sender, TextChangedEventArgs args)
        {
            textBox.SelectionStart = textBox.Text.Length;
        }

        private void TextBoxOnGotFocus(object sender, RoutedEventArgs routedEventArgs)
        {
            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcherOnThreadPreprocessMessage;
        }

        private void ComponentDispatcherOnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == UnsafeNative.Constants.WM_HOTKEY)
            {
                // swallow all hotkeys, so our control can catch the key strokes
                handled = true;
            }
        }

        private void TextBoxOnLostFocus(object sender, RoutedEventArgs routedEventArgs)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcherOnThreadPreprocessMessage;
        }

        private void TextBoxOnPreviewKeyDown2(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            switch (key)
            {
                case Key.Tab:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.RWin:
                case Key.LWin:
                    return;
            }

            e.Handled = true;

            var currentModifierKeys = GetCurrentModifierKeys();
            if (currentModifierKeys == ModifierKeys.None && key == Key.Back)
            {
                HotKey = null;
            }
            else if (currentModifierKeys != ModifierKeys.None || !AreModifierKeysRequired)
            {
                HotKey = new HotkeyGesture(key, currentModifierKeys);
            }

            UpdateText();
        }

        private static ModifierKeys GetCurrentModifierKeys()
        {
            var modifier = ModifierKeys.None;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                modifier |= ModifierKeys.Control;
            }

            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                modifier |= ModifierKeys.Alt;
            }

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                modifier |= ModifierKeys.Shift;
            }

            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            {
                modifier |= ModifierKeys.Windows;
            }

            return modifier;
        }

        private void UpdateText()
        {
            var hotkey = HotKey ?? new HotkeyGesture(Key.None);
            Text = hotkey.ToString();
        }
    }
}