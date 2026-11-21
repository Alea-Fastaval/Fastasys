namespace Fastasys.ApiService.Services;

/// <summary>
/// Reads uploaded images for the ID cards: what kind of image the bytes really are, and how large it is. Infosys
/// accepted PNG, JPEG and GIF and trusted the type the browser declared in the data URL; here the bytes decide.
/// </summary>
public static class ImageFiles
{
    // The largest image accepted (a phone photo is a few MB). Infosys had no limit.
    public const int MaxBytes = 10 * 1024 * 1024;

    public sealed record ImageInfo(string ContentType, int Width, int Height);

    // A data URL from the browser ("data:image/png;base64,...") as bytes, or null when it isn't a base64 data URL
    public static byte[]? FromDataUrl(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:", StringComparison.Ordinal)) return null;

        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || !dataUrl.AsSpan(0, comma).EndsWith(";base64", StringComparison.Ordinal)) return null;

        try
        {
            return Convert.FromBase64String(dataUrl[(comma + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // The image's type and size in pixels, or null when the bytes aren't a PNG, JPEG or GIF image
    public static ImageInfo? Inspect(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0 || data.Length > MaxBytes) return null;

        // PNG: the signature, then the IHDR chunk with the width and height (big-endian)
        if (data.Length >= 24 && data[..8].SequenceEqual(stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Sized("image/png", BigEndian32(data[16..]), BigEndian32(data[20..]));
        }

        // GIF: "GIF87a" or "GIF89a", then the width and height (little-endian)
        if (data.Length >= 10 && data[..3].SequenceEqual("GIF"u8) && (data[3..6].SequenceEqual("87a"u8) || data[3..6].SequenceEqual("89a"u8)))
        {
            return Sized("image/gif", data[6] | (data[7] << 8), data[8] | (data[9] << 8));
        }

        // JPEG: the start marker, then segments until a start-of-frame segment with the height and width
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
        {
            return JpegSize(data);
        }

        return null;
    }

    private static ImageInfo? JpegSize(ReadOnlySpan<byte> data)
    {
        var i = 2;
        while (i + 3 < data.Length)
        {
            if (data[i] != 0xFF) return null;

            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                // Fill byte
                i++;
                continue;
            }

            // Markers without a length: TEM and the restart markers
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                i += 2;
                continue;
            }

            var length = (data[i + 2] << 8) | data[i + 3];
            if (length < 2) return null;

            // Start of frame: SOF0-SOF15 except DHT (C4), JPG (C8) and DAC (CC)
            if (marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (i + 8 >= data.Length) return null;
                return Sized("image/jpeg", (data[i + 7] << 8) | data[i + 8], (data[i + 5] << 8) | data[i + 6]);
            }

            i += 2 + length;
        }

        return null;
    }

    private static ImageInfo? Sized(string contentType, long width, long height) =>
        width is > 0 and <= 20000 && height is > 0 and <= 20000 ? new ImageInfo(contentType, (int)width, (int)height) : null;

    private static long BigEndian32(ReadOnlySpan<byte> data) =>
        ((long)data[0] << 24) | ((long)data[1] << 16) | ((long)data[2] << 8) | data[3];
}
