namespace MicSwitch.MainWindow.Models
{
    internal struct HotkeyConfig
    {
        public string Key { get; set; }
        
        public string AlternativeKey { get; set; }
        
        public bool Suppress { get; set; }
    }
}