using Newtonsoft.Json;

namespace MicSwitch.Services
{
    internal struct MicrophoneLineData
    {
        public MicrophoneLineData(string lineId, string name)
        {
            LineId = lineId;
            Name = name;
        }

        public string LineId { get; }

        public string Name { get; }

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