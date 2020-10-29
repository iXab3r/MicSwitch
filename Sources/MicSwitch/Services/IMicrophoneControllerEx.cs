using PoeShared.Scaffolding;

namespace MicSwitch.Services
{
    internal interface IMicrophoneControllerEx : IMicrophoneController
    {
        new MicrophoneLineData LineId { get; set; }
    }
}