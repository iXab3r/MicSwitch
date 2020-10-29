using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using NAudio.CoreAudioApi;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Subjects;
using log4net;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Utils;
using PoeShared;
using PoeShared.Modularity;
using PoeShared.Scaffolding;

namespace MicSwitch.Services
{
    internal sealed class MicrophoneProvider : DisposableReactiveObject, IMicrophoneProvider
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MicrophoneProvider));

        private readonly TimeSpan ThrottlingTimeout = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan RetryTimeout = TimeSpan.FromSeconds(60);

        private readonly SourceList<MicrophoneLineData> microphoneLines = new SourceList<MicrophoneLineData>();
        private readonly NotificationClient notificationClient = new NotificationClient();
        private readonly MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();

        public MMDevice GetMixerControl(string lineId)
        {
            return EnumerateLinesInternal().FirstOrDefault(x => x.ID == lineId);
        }

        public MicrophoneProvider()
        {
            microphoneLines
                .Connect()
                .Bind(out var microphones)
                .Subscribe()
                .AddTo(Anchors);
            Microphones = microphones;

            Observable
                .Start(() =>
                {
                    Log.Debug($"Registering NotificationCallback using {deviceEnumerator}");
                    var hResult = deviceEnumerator.RegisterEndpointNotificationCallback(notificationClient);
                    if (hResult != HResult.S_OK)
                    {
                        throw new ApplicationException($"Failed to subscribe to Notifications using {deviceEnumerator}, hResult: {hResult}");
                    }
                    Log.Debug($"Successfully subscribed to Notifications using {deviceEnumerator}");
                })
                .RetryWithDelay(RetryTimeout)
                .Subscribe()
                .AddTo(Anchors);

            Observable.Merge(
                    Observable.Timer(DateTimeOffset.Now, RetryTimeout).ToUnit(),
                    notificationClient.WhenDeviceAdded.Do(deviceId => Log.Debug($"[Notification] Device added, id: {deviceId}")).ToUnit(),
                    notificationClient.WhenDeviceStateChanged.Do(x => Log.Debug($"[Notification] Device state changed, id: {x.deviceId}, state: {x.newState}")).ToUnit(),
                    notificationClient.WhenDeviceRemoved.Do(deviceId => Log.Debug($"[Notification] Device removed, id: {deviceId}")).ToUnit())
                .Throttle(ThrottlingTimeout)
                .Select(x => EnumerateLines())
                .DistinctUntilChanged(x => x.DumpToText())
                .Subscribe(newLines =>
                {
                    Log.Debug($"Microphone lines list changed:\n\tCurrent lines list:\n\t\t{microphoneLines.Items.DumpToTable("\n\t\t")}\n\tNew lines list:\n\t\t{newLines.DumpToTable("\n\t\t")}");
                    var linesToAdd = newLines.Except(microphoneLines.Items).ToArray();
                    if (linesToAdd.Any())
                    {
                        Log.Debug($"Adding microphone lines: {linesToAdd.DumpToTextRaw()}");
                        microphoneLines.AddRange(linesToAdd);
                    }

                    var linesToRemove = microphoneLines.Items.Except(newLines).ToArray();
                    if (linesToRemove.Any())
                    {
                        Log.Debug($"Removing microphone lines: {linesToRemove.DumpToTextRaw()}");
                        microphoneLines.RemoveMany(linesToRemove);
                    }
                }, Log.HandleUiException)
                .AddTo(Anchors);
        }

        public ReadOnlyObservableCollection<MicrophoneLineData> Microphones { get; }

        public IEnumerable<MicrophoneLineData> EnumerateLines()
        {
            yield return MicrophoneLineData.All;

            var devices = EnumerateLinesInternal();
            foreach (var device in devices)
            {
                yield return new MicrophoneLineData(lineId: device.ID, name: device.FriendlyName);
            }
        }

        private IEnumerable<MMDevice> EnumerateLinesInternal()
        {
            var devices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            return devices;
        }

        private sealed class NotificationClient : IMMNotificationClient
        {
            private readonly ISubject<string> whenDeviceRemoved = new Subject<string>();
            private readonly ISubject<string> whenDeviceAdded = new Subject<string>();
            private readonly ISubject<(string deviceId, DeviceState newState)> whenDeviceStateChanged = new Subject<(string deviceId, DeviceState newState)>();
            private readonly ISubject<(string defaultDeviceId, DataFlow flow, Role role)> whenDefaultDeviceChanged = new Subject<(string defaultDeviceId, DataFlow flow, Role role)>();
            private readonly ISubject<(string pwstrDeviceId, PropertyKey key)> whenPropertyValueChanged = new Subject<(string pwstrDeviceId, PropertyKey key)>();

            public IObservable<string> WhenDeviceRemoved => whenDeviceRemoved;

            public IObservable<string> WhenDeviceAdded => whenDeviceAdded;

            public IObservable<(string deviceId, DeviceState newState)> WhenDeviceStateChanged => whenDeviceStateChanged;

            public IObservable<(string defaultDeviceId, DataFlow flow, Role role)> WhenDefaultDeviceChanged => whenDefaultDeviceChanged;

            public IObservable<(string pwstrDeviceId, PropertyKey key)> WhenPropertyValueChanged => whenPropertyValueChanged;

            public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            {
                whenDeviceStateChanged.OnNext((deviceId: deviceId, newState: newState));
            }

            public void OnDeviceAdded(string pwstrDeviceId)
            {
                whenDeviceAdded.OnNext(pwstrDeviceId);
            }

            public void OnDeviceRemoved(string deviceId)
            {
                whenDeviceRemoved.OnNext(deviceId);
            }

            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            {
                whenDefaultDeviceChanged.OnNext((defaultDeviceId: defaultDeviceId, flow: flow, role: role));
            }

            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
            {
                whenPropertyValueChanged.OnNext((pwstrDeviceId: pwstrDeviceId, key: key));
            }
        }
    }
}