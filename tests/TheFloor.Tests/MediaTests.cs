using TheFloor.Content;
using Xunit;

namespace TheFloor.Tests;

public sealed class MediaTests
{
    [Theory]
    [InlineData("photo.jpg", "image/jpeg", "FFD8FF")]
    [InlineData("photo.jpeg", "image/jpeg", "FFD8FF")]
    [InlineData("photo.png", "image/png", "89504E470D0A1A0A")]
    [InlineData("photo.webp", "image/webp", "524946460000000057454250")]
    [InlineData("photo.gif", "image/gif", "474946383961")]
    [InlineData("audio.mp3", "audio/mpeg", "494433")]
    [InlineData("audio.wav", "audio/x-wav", "524946460000000057415645")]
    [InlineData("audio.ogg", "application/ogg", "4F676753")]
    public void VerifiesMediaSignaturesAndMimeTypes(string file, string mime, string signature)
    {
        var header = Convert.FromHexString(signature);
        Media.Validate(file, header, header.Length, mime);
        Assert.Throws<ContentException>(() => Media.Validate(file, new byte[header.Length], header.Length, mime));
        Assert.Throws<ContentException>(() => Media.Validate(file, header, header.Length, "text/html"));
        Assert.Throws<ContentException>(() => Media.Validate(file, header, Media.MaxBytes + 1L, mime));
    }

    [Fact]
    public void AcceptsShortMp4AndWebMClipsAndRejectsLongOrUnreadableOnes()
    {
        Media.Validate("clip.mp4", Mp4(3), Mp4(3).Length, "video/mp4");
        Media.Validate("clip.mp4", Mp4(5), Mp4(5).Length, "video");
        Media.Validate("clip.webm", WebM(2.5), WebM(2.5).Length, "video/webm");
        Assert.Throws<ContentException>(() => Media.Validate("clip.mp4", Mp4(6), Mp4(6).Length, "video/mp4"));
        Assert.Throws<ContentException>(() => Media.Validate("clip.webm", WebM(12), WebM(12).Length, "video/webm"));
        Assert.Throws<ContentException>(() => Media.Validate("clip.mp4", Mp4(3), Mp4(3).Length, "image/png"));
        Assert.Throws<ContentException>(() => Media.Validate("clip.mp4", WebM(2), WebM(2).Length, "video/mp4"));
        var noDuration = Mp4(3)[..16];
        Assert.Throws<ContentException>(() => Media.Validate("clip.mp4", noDuration, noDuration.Length, "video/mp4"));
        var tooLarge = new byte[Media.MaxVideoBytes + 1];
        Assert.Throws<ContentException>(() => Media.Validate("clip.mp4", tooLarge, tooLarge.Length, "video/mp4"));
    }

    [Fact]
    public void VerifiesStoredVideoFiles()
    {
        var folder = Directory.CreateTempSubdirectory("floor-video").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "ok.mp4"), Mp4(2));
            File.WriteAllBytes(Path.Combine(folder, "long.webm"), WebM(30));
            Media.VerifyFile(folder, "ok.mp4", "video");
            Assert.Throws<ContentException>(() => Media.VerifyFile(folder, "long.webm", "video"));
        }
        finally { Directory.Delete(folder, true); }
    }

    // ftyp box followed by moov/mvhd (version 0, timescale 1000).
    private static byte[] Mp4(double seconds)
    {
        var mvhd = new List<byte>();
        mvhd.AddRange(new byte[12]);
        mvhd.AddRange(Be(1000));
        mvhd.AddRange(Be((uint)(seconds * 1000)));
        mvhd.AddRange(new byte[80]);
        return [.. Box("ftyp", "isom\0\0\0\0"u8.ToArray()), .. Box("moov", Box("mvhd", mvhd.ToArray()))];
    }

    // EBML header, then an unknown-size Segment holding Info with TimecodeScale and an 8-byte float Duration (ms).
    private static byte[] WebM(double seconds)
    {
        byte[] info = [0x2A, 0xD7, 0xB1, 0x83, 0x0F, 0x42, 0x40, 0x44, 0x89, 0x88, .. BitConverter.GetBytes(seconds * 1000).Reverse()];
        return [0x1A, 0x45, 0xDF, 0xA3, 0x80, 0x18, 0x53, 0x80, 0x67, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x15, 0x49, 0xA9, 0x66, (byte)(0x80 | info.Length), .. info];
    }

    private static byte[] Be(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] Box(string type, byte[] body) =>
        [.. Be((uint)(body.Length + 8)), .. System.Text.Encoding.ASCII.GetBytes(type), .. body];
}
