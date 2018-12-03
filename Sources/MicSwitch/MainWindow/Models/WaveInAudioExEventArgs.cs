using System;
using NAudio.Wave;

namespace MicSwitch.MainWindow.Models
{
    internal class WaveInAudioExEventArgs : EventArgs
    {
        /// <summary>Creates new WaveInEventArgs</summary>
        public WaveInAudioExEventArgs(byte[] buffer, int bytes, WaveFormat dataFormat)
        {
            Buffer = buffer;
            BytesRecorded = bytes;
            DataFormat = dataFormat;
        }

        /// <summary>
        ///     Buffer containing recorded data. Note that it might not be completely
        ///     full. <seealso cref="P:NAudio.Wave.WaveInEventArgs.BytesRecorded" />
        /// </summary>
        public byte[] Buffer { get; }

        /// <summary>
        ///     The number of recorded bytes in Buffer. <seealso cref="P:NAudio.Wave.WaveInEventArgs.Buffer" />
        /// </summary>
        public int BytesRecorded { get; }

        public WaveFormat DataFormat { get; }
    }
}