using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace MicSwitch.MainWindow.Models
{
    internal sealed class MicrophoneProvider
    {
        public MMDevice GetMixerControl(string lineId)
        {
            return EnumerateLinesInternal().FirstOrDefault(x => x.ID == lineId);
        }
        
        public IEnumerable<MicrophoneLineData> EnumerateLines()
        {
            var devices = EnumerateLinesInternal();
            foreach (var device in devices)
            {
                yield return new MicrophoneLineData()
                {
                    LineId = device.ID,
                    Name = device.FriendlyName
                };
            }
        }
        
        public IEnumerable<MMDevice> EnumerateLinesInternal()
        {
            var de = new MMDeviceEnumerator();
            var devices = de.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            return devices;
        }
    }
}