using System.Windows.Input;
using MicSwitch.WPF.Hotkeys;
using NUnit.Framework;
using Shouldly;

namespace MicSwitch.Tests.WPF.Hotkeys
{
    [TestFixture]
    public class HotkeyTests
    {
        [TestCase(Key.None, ModifierKeys.None, "None")]
        [TestCase(Key.A, ModifierKeys.None, "A")]
        [TestCase(Key.None, ModifierKeys.Control, "Ctrl")]
        [TestCase(Key.LeftCtrl, ModifierKeys.None, "Ctrl")]
        [TestCase(Key.RightCtrl, ModifierKeys.None, "Ctrl")]
        [TestCase(Key.RightCtrl, ModifierKeys.Control, "Ctrl")]
        [TestCase(Key.A, ModifierKeys.Control, "Ctrl+A")]
        [TestCase(Key.A, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows, "Ctrl+Alt+Shift+Windows+A")]
        public void ShouldSerializeKeyboard(Key key, ModifierKeys modifierKeys, string expected)
        {
            // Given
            var hotkey = new HotkeyGesture(key, modifierKeys);

            // When
            var toString = hotkey.ToString();

            // Then
            toString.ShouldBe(expected);
        }

        [TestCase(Key.None, ModifierKeys.None, Key.None, ModifierKeys.None)]
        [TestCase(Key.LeftAlt, ModifierKeys.None, Key.None, ModifierKeys.Alt)]
        [TestCase(Key.LeftAlt, ModifierKeys.Control, Key.None, ModifierKeys.Control | ModifierKeys.Alt)]
        [TestCase(Key.LeftCtrl, ModifierKeys.Alt, Key.None, ModifierKeys.Alt | ModifierKeys.Control)]
        [TestCase(Key.LeftCtrl, ModifierKeys.Control, Key.None, ModifierKeys.Control)]
        public void ShouldNormalize(Key key, ModifierKeys modifierKeys, Key expectedKey, ModifierKeys expectedModifierKeys)
        {
            // Given
            // When
            var hotkey = new HotkeyGesture(key, modifierKeys);

            // Then
            hotkey.Key.ShouldBe(expectedKey, () => hotkey.ToString());
            hotkey.ModifierKeys.ShouldBe(expectedModifierKeys, () => hotkey.ToString());
        }

        [TestCase(MouseButton.Left, ModifierKeys.None, "MouseLeft")]
        [TestCase(MouseButton.Left, ModifierKeys.Control, "Ctrl+MouseLeft")]
        [TestCase(MouseButton.XButton2, ModifierKeys.Control, "Ctrl+MouseXButton2")]
        public void ShouldSerializeMouse(MouseButton key, ModifierKeys modifierKeys, string expected)
        {
            // Given
            var hotkey = new HotkeyGesture(key, modifierKeys);

            // When
            var toString = hotkey.ToString();

            // Then
            toString.ShouldBe(expected);
        }
    }
}