using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Common.Logging;
using Guards;
using JetBrains.Annotations;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Prism;
using PoeShared.Scaffolding;
using PoeShared.Scaffolding.WPF;
using PoeShared.UI;
using ReactiveUI;
using Unity.Attributes;

namespace MicSwitch.Updater
{
    internal sealed class ApplicationUpdaterViewModel : DisposableReactiveObject
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ApplicationUpdaterViewModel));

        private readonly IApplicationUpdaterModel updaterModel;
        private string error = string.Empty;

        private bool isOpen;

        private string statusText;

        public ApplicationUpdaterViewModel(
            [NotNull] IApplicationUpdaterModel updaterModel,
            [NotNull] IConfigProvider<UpdateSettingsConfig> configProvider,
            [NotNull] [Dependency(WellKnownSchedulers.UI)] IScheduler uiScheduler,
            [NotNull] [Dependency(WellKnownSchedulers.Background)] IScheduler bgScheduler)
        {
            Guard.ArgumentNotNull(updaterModel, nameof(updaterModel));
            Guard.ArgumentNotNull(uiScheduler, nameof(uiScheduler));
            Guard.ArgumentNotNull(bgScheduler, nameof(bgScheduler));
            Guard.ArgumentNotNull(configProvider, nameof(configProvider));

            this.updaterModel = updaterModel;

            updaterModel.WhenAnyValue(x => x.UpdatedVersion)
                        .Subscribe(() => this.RaisePropertyChanged(nameof(UpdatedVersion)))
                        .AddTo(Anchors);

            CheckForUpdatesCommand = CommandWrapper
                .Create(CheckForUpdatesCommandExecuted);

            CheckForUpdatesCommand
                .ThrownExceptions
                .Subscribe(ex => Error = $"Update error: {ex.Message}")
                .AddTo(Anchors);

            updaterModel
                .WhenAnyValue(x => x.UpdatedVersion)
                .ObserveOn(uiScheduler)
                .Subscribe(() => this.RaisePropertyChanged(nameof(UpdatedVersion)), Log.HandleUiException)
                .AddTo(Anchors);

            updaterModel
                .WhenAnyValue(x => x.LatestVersion)
                .ObserveOn(uiScheduler)
                .Subscribe(() => this.RaisePropertyChanged(nameof(LatestVersion)), Log.HandleUiException)
                .AddTo(Anchors);

            //FIXME UI THREAD ?
            RestartCommand = CommandWrapper
                .Create(RestartCommandExecuted);

            RestartCommand
                .ThrownExceptions
                .Subscribe(ex => Error = $"Restart error: {ex.Message}")
                .AddTo(Anchors);

            configProvider
                .ListenTo(x => x.UpdateSource)
                .Subscribe(x => updaterModel.UpdateSource = x)
                .AddTo(Anchors);

            configProvider
                .ListenTo(x => x.AutoUpdateTimeout)
                .WithPrevious((prev, curr) => new {prev, curr})
                .Do(timeout => Log.Debug($"[ApplicationUpdaterViewModel] AutoUpdate timeout changed: {timeout.prev} => {timeout.curr}"))
                .Select(timeout => timeout.curr <= TimeSpan.Zero
                            ? Observable.Never<long>()
                            : Observable.Timer(DateTimeOffset.MinValue, timeout.curr, bgScheduler))
                .Switch()
                .ObserveOn(uiScheduler)
                .Subscribe(() => CheckForUpdatesCommand.Execute(null), Log.HandleException)
                .AddTo(Anchors);

            ApplyUpdate = CommandWrapper.Create(
                ApplyUpdateCommandExecuted,
                this.updaterModel.WhenAnyValue(x => x.LatestVersion).ObserveOn(uiScheduler).Select(x => x != null));
        }

        public CommandWrapper CheckForUpdatesCommand { get; }

        public CommandWrapper RestartCommand { get; }
    
        public CommandWrapper ApplyUpdate { get; }

        public string Error
        {
            get => error;
            set => this.RaiseAndSetIfChanged(ref error, value);
        }

        public string StatusText
        {
            get => statusText;
            set => this.RaiseAndSetIfChanged(ref statusText, value);
        }

        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }

        [CanBeNull]
        public Version UpdatedVersion => updaterModel.UpdatedVersion;

        [CanBeNull]
        public Version LatestVersion => updaterModel.LatestVersion?.FutureReleaseEntry?.Version?.Version;

        private async Task CheckForUpdatesCommandExecuted()
        {
            Log.Debug("[ApplicationUpdaterViewModel] Update check requested");
            if (CheckForUpdatesCommand.IsBusy || ApplyUpdate.IsBusy)
            {
                Log.Debug("[ApplicationUpdaterViewModel] Already in progress");
                IsOpen = true;
                return;
            }

            StatusText = "Checking for updates...";
            Error = string.Empty;
            updaterModel.Reset();

            // delaying update so the user could see the progress ring
            await Task.Delay(UiConstants.ArtificialLongDelay);
            
            try
            {
                var newVersion = await Task.Run(updaterModel.CheckForUpdates);
                
                if (newVersion != null)
                {
                    IsOpen = true;
                    StatusText = $"New {LatestVersion} version available";
                }
                else
                {
                    StatusText = "Latest version is already installed";
                }
            }
            catch (Exception ex)
            {
                Log.HandleUiException(ex);
                IsOpen = true;
                Error = ex.Message;
            }
        }

        private async Task ApplyUpdateCommandExecuted()
        {
            Log.Debug($"[ApplicationUpdaterViewModel] Applying latest update {LatestVersion}");
            if (CheckForUpdatesCommand.IsBusy || ApplyUpdate.IsBusy)
            {
                Log.Debug("[ApplicationUpdaterViewModel] Already in progress");
                IsOpen = true;
                return;
            }

            StatusText = $"Applying update {LatestVersion}...";
            Error = string.Empty;

            if (updaterModel.LatestVersion == null)
            {
                throw new ApplicationException("Latest version must be specified");
            }

            await Task.Delay(UiConstants.ArtificialLongDelay);

            try
            {
                await updaterModel.ApplyRelease(updaterModel.LatestVersion);
                IsOpen = true;
                StatusText = $"Success !";
            }
            catch (Exception ex)
            {
                Log.HandleUiException(ex);
                IsOpen = true;
                Error = ex.Message;
                StatusText = null;
            }
        }

        private async Task RestartCommandExecuted()
        {
            Log.Debug("[ApplicationUpdaterViewModel] Restart application requested");
            Error = string.Empty;

            try
            {
                IsOpen = true;
                StatusText = "Restarting application...";

                await updaterModel.RestartApplication();
            }
            catch (Exception ex)
            {
                IsOpen = true;

                Log.HandleUiException(ex);
                Error = ex.Message;
                StatusText = null;
            }
        }
    }
}