using System.Buffers.Binary;
using System.Numerics;

namespace TheFloor.Content;

/// <summary>Reads clip length from MP4 and WebM headers without decoding the video.</summary>
internal static class VideoDuration
{
    public static double? Mp4(ReadOnlySpan<byte> data)
    {
        if (!FindBox(data, "moov"u8, out var moov) || !FindBox(moov, "mvhd"u8, out var header) || header.Length < 20)
            return null;
        double scale, ticks;
        if (header[0] == 1)
        {
            if (header.Length < 32) return null;
            scale = BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
            ticks = BinaryPrimitives.ReadUInt64BigEndian(header[24..]);
        }
        else
        {
            scale = BinaryPrimitives.ReadUInt32BigEndian(header[12..]);
            ticks = BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        }
        return scale > 0 ? ticks / scale : null;
    }

    private static bool FindBox(ReadOnlySpan<byte> data, ReadOnlySpan<byte> type, out ReadOnlySpan<byte> body)
    {
        body = default;
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            var headerLength = 8;
            if (size == 1)
            {
                if (offset + 16 > data.Length) return false;
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(data[(offset + 8)..]);
                headerLength = 16;
            }
            else if (size == 0) size = data.Length - offset;
            if (size < headerLength || offset + size > data.Length) return false;
            if (data.Slice(offset + 4, 4).SequenceEqual(type))
            {
                body = data.Slice(offset + headerLength, (int)size - headerLength);
                return true;
            }
            offset += (int)size;
        }
        return false;
    }

    public static double? WebM(ReadOnlySpan<byte> data)
    {
        var state = new WebMInfo();
        Walk(data, 0, ref state);
        return state.Duration >= 0 ? state.Duration * state.Scale / 1_000_000_000 : null;
    }

    private struct WebMInfo
    {
        public double Scale = 1_000_000;
        public double Duration = -1;
        public WebMInfo() { }
    }

    private static void Walk(ReadOnlySpan<byte> level, int depth, ref WebMInfo state)
    {
        var offset = 0;
        while (offset < level.Length && depth < 4)
        {
            if (!Vint(level, ref offset, keepMarker: true, out var id) ||
                !Vint(level, ref offset, keepMarker: false, out var size)) return;
            var length = size < 0 || offset + size > level.Length ? level.Length - offset : (int)size;
            var body = level.Slice(offset, length);
            switch (id)
            {
                case 0x18538067 or 0x1549A966: Walk(body, depth + 1, ref state); break; // Segment, Info
                case 0x2AD7B1: state.Scale = Unsigned(body); break;                      // TimecodeScale
                case 0x4489 when length == 4: state.Duration = BinaryPrimitives.ReadSingleBigEndian(body); break;
                case 0x4489 when length == 8: state.Duration = BinaryPrimitives.ReadDoubleBigEndian(body); break;
                case 0x1F43B675: return; // Cluster: Info always precedes the media data
            }
            offset += length;
        }
    }

    private static double Unsigned(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        foreach (var b in bytes) value = (value << 8) | b;
        return value;
    }

    private static bool Vint(ReadOnlySpan<byte> data, ref int offset, bool keepMarker, out long value)
    {
        value = 0;
        if (offset >= data.Length || data[offset] == 0) return false;
        var length = BitOperations.LeadingZeroCount((uint)data[offset]) - 24 + 1;
        if (offset + length > data.Length) return false;
        var first = data[offset];
        var raw = (long)(keepMarker ? first : first & (0xff >> length));
        var unknown = (first & (0xff >> length)) == (0xff >> length);
        for (var i = 1; i < length; i++)
        {
            raw = (raw << 8) | data[offset + i];
            unknown &= data[offset + i] == 0xff;
        }
        offset += length;
        value = !keepMarker && unknown ? -1 : raw;
        return true;
    }
}
