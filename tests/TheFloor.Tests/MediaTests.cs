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
}
