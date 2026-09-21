using System;
namespace Tang.RadarApplication.Models
{
    public sealed class CanFrame
    {
        public uint Id { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
