using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// The uploaded images of the ID cards: what they really are and how large they are
public class ImageFilesTests
{
    [Fact]
    public void Inspect_ReadsAPng()
    {
        Assert.Equal(new ImageFiles.ImageInfo("image/png", 1011, 638), ImageFiles.Inspect(TestImages.Png(1011, 638)));
    }

    [Fact]
    public void Inspect_ReadsAGif()
    {
        Assert.Equal(new ImageFiles.ImageInfo("image/gif", 300, 200), ImageFiles.Inspect(TestImages.Gif(300, 200)));
    }

    [Fact]
    public void Inspect_ReadsAJpegFromItsStartOfFrame()
    {
        Assert.Equal(new ImageFiles.ImageInfo("image/jpeg", 213, 295), ImageFiles.Inspect(TestImages.Jpeg(213, 295)));
    }

    [Fact]
    public void Inspect_RefusesWhatIsntAPngJpegOrGif()
    {
        Assert.Null(ImageFiles.Inspect("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8));
        Assert.Null(ImageFiles.Inspect("%PDF-1.7"u8));
        Assert.Null(ImageFiles.Inspect([]));
        // A PNG that says it has no pixels
        Assert.Null(ImageFiles.Inspect(TestImages.Png(0, 10)));
    }

    [Fact]
    public void Inspect_RefusesImagesOver10Mb()
    {
        var big = new byte[ImageFiles.MaxBytes + 1];
        TestImages.Png(10, 10).CopyTo(big, 0);

        Assert.Null(ImageFiles.Inspect(big));
    }

    [Fact]
    public void FromDataUrl_DecodesBase64DataUrls()
    {
        var png = TestImages.Png(2, 3);

        Assert.Equal(png, ImageFiles.FromDataUrl(TestImages.DataUrl(png)));
        Assert.Null(ImageFiles.FromDataUrl("data:image/png,not-base64"));
        Assert.Null(ImageFiles.FromDataUrl("data:image/png;base64,***"));
        Assert.Null(ImageFiles.FromDataUrl("https://example.invalid/photo.png"));
        Assert.Null(ImageFiles.FromDataUrl(null));
    }

    [Fact]
    public void TheBytesDecideTheType_NotTheDataUrl()
    {
        // Infosys trusted the type the browser declared; here a GIF declared as PNG is a GIF
        var bytes = ImageFiles.FromDataUrl(TestImages.DataUrl(TestImages.Gif(5, 5), "image/png"));

        Assert.Equal("image/gif", ImageFiles.Inspect(bytes!)!.ContentType);
    }
}
