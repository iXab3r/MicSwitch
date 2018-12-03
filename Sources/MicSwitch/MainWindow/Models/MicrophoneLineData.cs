using JsonFx.Json;

namespace MicSwitch.MainWindow.Models
{
    internal struct MicrophoneLineData
    {
        public static readonly MicrophoneLineData Empty = new MicrophoneLineData {Name = "No name"};

        public string LineId { get; set; }

        public string Name { get; set; }

        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrEmpty(LineId);

        public override string ToString()
        {
            return $"{nameof(LineId)}: {LineId}, {nameof(Name)}: {Name}";
        }

        public bool Equals(MicrophoneLineData other)
        {
            return string.Equals(LineId, other.LineId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is MicrophoneLineData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return LineId != null ? LineId.GetHashCode() : 0;
        }
    }
}