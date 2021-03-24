namespace MicSwitch.MainWindow.Models
{
    internal sealed record HotkeyConfig
    {
        public static readonly HotkeyConfig Empty = new();
        
        public string Key { get; set; }
        
        public string AlternativeKey { get; set; }

        public bool Suppress { get; set; } = true;
    }
}