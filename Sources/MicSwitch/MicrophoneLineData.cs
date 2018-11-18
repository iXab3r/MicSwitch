namespace MicSwitch
{
    internal sealed class MicrophoneLineData
    {
        public static readonly MicrophoneLineData Empty = new MicrophoneLineData(){ Name = "Not selected" };
        
        public string LineId { get; set; }
        
        public string Name { get; set; }

        private bool Equals(MicrophoneLineData other)
        {
            return string.Equals(LineId, other.LineId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is MicrophoneLineData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (LineId != null ? LineId.GetHashCode() : 0);
        }
    }
}