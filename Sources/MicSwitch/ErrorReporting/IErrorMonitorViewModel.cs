using PoeShared.Scaffolding.WPF;

namespace MicSwitch.ErrorReporting
{
    internal interface IErrorMonitorViewModel
    {
        CommandWrapper ReportProblemCommand { get; }
        CommandWrapper ThrowExceptionCommand { get; }
    }
}