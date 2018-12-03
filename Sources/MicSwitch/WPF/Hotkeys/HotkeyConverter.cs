using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace MicSwitch.WPF.Hotkeys
{
    public class HotkeyConverter : System.ComponentModel.TypeConverter
    {
        private const char ModifiersDelimiter = '+';
        private static readonly KeyConverter KeyConverter = new KeyConverter();
        private static readonly ModifierKeysConverter ModifierKeysConverter = new ModifierKeysConverter();

        private readonly IDictionary<string, HotkeyGesture> mouseKeys;

        public HotkeyConverter()
        {
            mouseKeys = Enum
                .GetValues(typeof(MouseButton))
                .OfType<MouseButton>()
                .Select(x => new HotkeyGesture(x))
                .ToDictionary(x => x.ToString(), x => x, StringComparer.OrdinalIgnoreCase);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType != typeof(string) || context?.Instance == null)
            {
                return false;
            }

            if (context.Instance is HotkeyGesture instance && ModifierKeysConverter.IsDefinedModifierKeys(instance.ModifierKeys))
            {
                return IsDefinedKey(instance.Key);
            }

            return false;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object sourceRaw)
        {
            if (!(sourceRaw is string))
            {
                throw GetConvertFromException(sourceRaw);
            }

            var source = ((string) sourceRaw).Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                return new HotkeyGesture(Key.None);
            }

            var modifiersPartLength = source.LastIndexOf(ModifiersDelimiter);
            string modifiersPartRaw;
            string hotkeyPartRaw;
            if (modifiersPartLength >= 0)
            {
                modifiersPartRaw = source.Substring(0, modifiersPartLength);
                hotkeyPartRaw = source.Substring(modifiersPartLength + 1);
            }
            else
            {
                modifiersPartRaw = string.Empty;
                hotkeyPartRaw = source;
            }

            var modifiersRaw = ModifierKeysConverter.ConvertFrom(context, culture, modifiersPartRaw);
            var modifiers = (ModifierKeys) modifiersRaw;

            if (mouseKeys.ContainsKey(hotkeyPartRaw))
            {
                var mouseKey = mouseKeys[hotkeyPartRaw];
                return new HotkeyGesture(mouseKey.MouseButton.Value, modifiers);
            }

            var key = KeyConverter.ConvertFrom(context, culture, hotkeyPartRaw);
            if (key == null)
            {
                throw GetConvertFromException(sourceRaw);
            }

            return new HotkeyGesture((Key) key, modifiers);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (destinationType != typeof(string))
            {
                throw GetConvertToException(value, destinationType);
            }

            if (value == null)
            {
                return string.Empty;
            }

            if (!(value is HotkeyGesture keyGesture))
            {
                throw GetConvertToException(value, destinationType);
            }

            return keyGesture.ToString();
        }

        private static bool IsDefinedKey(Key key)
        {
            if (key >= Key.None)
            {
                return key <= Key.OemClear;
            }

            return false;
        }
    }
}