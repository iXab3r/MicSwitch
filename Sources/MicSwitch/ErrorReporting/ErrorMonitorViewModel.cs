using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using PoeShared.Modularity;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;
using PoeShared.UI;
using PropertyBinder;
using Unity;

namespace MicSwitch.ErrorReporting
{
    internal sealed class ErrorMonitorViewModel : DisposableReactiveObject, IErrorMonitorViewModel
    {
        private static readonly Binder<ErrorMonitorViewModel> Binder = new();
        private readonly IExceptionDialogDisplayer exceptionDialogDisplayer;
        private readonly IExceptionReportingService exceptionReportingService;

        static ErrorMonitorViewModel()
        {
        }

        public ErrorMonitorViewModel(
            IAppArguments appArguments,
            IExceptionDialogDisplayer exceptionDialogDisplayer, 
            IExceptionReportingService exceptionReportingService,
            [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler)
        {
            this.exceptionDialogDisplayer = exceptionDialogDisplayer;
            this.exceptionReportingService = exceptionReportingService;
            ReportProblemCommand = CommandWrapper.Create(ReportProblemCommandExecuted);
            ThrowExceptionCommand = appArguments.IsDebugMode ? CommandWrapper.Create(() => uiScheduler.Schedule(() => throw new ApplicationException("Exception thrown on UI scheduler"))) : default;
            
            Binder.Attach(this).AddTo(Anchors);
        }

        public CommandWrapper ReportProblemCommand { get; }
        
        public CommandWrapper ThrowExceptionCommand { get; }

        private async Task ReportProblemCommandExecuted()
        {
            var config = await exceptionReportingService.PrepareConfig() with
            {
                Title = "Report a problem"
            };
            exceptionDialogDisplayer.ShowDialog(config);
        }
    }
}