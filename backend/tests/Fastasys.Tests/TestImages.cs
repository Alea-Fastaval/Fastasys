namespace Fastasys.Tests;

// The first bytes of PNG, GIF and JPEG images with a given size: enough for ImageFiles to recognise them. No real
// photos in the tests.
internal static class TestImages
{
    public static byte[] Png(int width, int height) =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
        (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
        (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
        8, 6, 0, 0, 0, 0, 0, 0, 0
    ];

    public static byte[] Gif(int width, int height) =>
    [
        (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
        (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8),
        0, 0, 0
    ];

    // SOI, an APP0 segment, then a baseline start-of-frame segment
    public static byte[] Jpeg(int width, int height) =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0,
        0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3,
        1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1
    ];

    public static string DataUrl(byte[] bytes, string declaredType = "image/png") =>
        $"data:{declaredType};base64,{Convert.ToBase64String(bytes)}";
}
