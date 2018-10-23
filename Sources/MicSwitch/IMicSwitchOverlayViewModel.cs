using System.Windows.Input;
using PoeShared.Native;

namespace MicSwitch
{
    internal interface IMicSwitchOverlayViewModel : IOverlayViewModel
    {
        bool Mute { get; }
        
        double ListScaleFactor { get; set; }
    }
}