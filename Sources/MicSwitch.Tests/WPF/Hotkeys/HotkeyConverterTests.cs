using System.Windows.Input;
using MicSwitch.WPF.Hotkeys;
using NUnit.Framework;
using Shouldly;

namespace MicSwitch.Tests.WPF.Hotkeys
{
    [TestFixture]
    public class HotkeyConverterTests
    {
        [TestCase(Key.None, ModifierKeys.None, "None")]
        [TestCase(Key.A, ModifierKeys.None, "A")]
        [TestCase(Key.None, ModifierKeys.Control, "Ctrl")]
        [TestCase(Key.A, ModifierKeys.Control, "Ctrl+A")]
        [TestCase(Key.A, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows, "Ctrl+Alt+Shift+Windows+A")]
        public void ShouldSerializeKeyboard(Key key, ModifierKeys modifierKeys, string expected)
        {
            // Given
            var instance = CreateInstance();
            var hotkey = new HotkeyGesture(key, modifierKeys);

            // When
            var result = instance.ConvertFrom(hotkey.ToString());

            // Then
            result.ShouldNotBeNull();
            result.ShouldBeOfType<HotkeyGesture>();
            result.ShouldBe(hotkey);
            result.ToString().ShouldBe(hotkey.ToString());
        }

        private HotkeyConverter CreateInstance()
        {
            return new HotkeyConverter();
        }
    }
}