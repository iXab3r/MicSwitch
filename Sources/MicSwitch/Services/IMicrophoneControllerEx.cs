using PoeShared.Audio.Models;

namespace MicSwitch.Services
{
    internal interface IMicrophoneControllerEx : IMicrophoneController
    {
        new MicrophoneLineData LineId { get; set; }
    }
}