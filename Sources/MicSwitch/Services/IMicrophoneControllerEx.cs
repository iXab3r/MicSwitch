using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMicrophoneControllerEx : IMicrophoneController
    {
        new MMDeviceLineData LineId { get; set; }
    }
}