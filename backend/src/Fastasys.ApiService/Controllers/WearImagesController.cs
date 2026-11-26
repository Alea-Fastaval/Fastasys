using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// An uploaded image in the edit form's "Tilføj billede" list
public record WearImageDto(int Id, string FileName);

// "Upload billede til wear": the chosen file's name and the file as a data URL ("data:image/png;base64,...")
public record CreateWearImageDto(string? FileName, string? Data);

// Infosys WearController::uploadImage and WearModel::getImages. Infosys kept the files in its public uploads folder
// under the name they were uploaded with, so a file with a PHP name and an image header could be run on the server;
// here the images are stored in the database behind login, and the bytes must be a PNG, JPEG or GIF image of at most
// 10 MB (agreed 2026-11-26). Infosys has no way to delete an image. The signup's public access comes with the signup
// endpoints.
[ApiController]
[Route("api/wear-images")]
[Authorize]
public class WearImagesController(InfosysDbContext db, ILogger<WearImagesController> logger) : ControllerBase
{
    // The reasons tell the page which message to show
    private static readonly object NotImage = new
    {
        message = "The file must be a PNG, JPEG or GIF image of at most 10 MB",
        reason = "notImage",
    };

    private static readonly object NameTaken = new
    {
        message = "There is already an image with that file name",
        reason = "nameTaken",
    };

    // The images by file name (WearModel::getImages orders by the file path)
    [HttpGet]
    [Authorize(Policy = "WearManagement")]
    public async Task<ActionResult<IEnumerable<WearImageDto>>> GetAll()
    {
        var images = await db.WearImages.AsNoTracking().Select(i => new WearImageDto(i.Id, i.FileName)).ToListAsync();
        return Ok(images.OrderBy(i => i.FileName, StringComparer.Ordinal).ThenBy(i => i.Id).ToList());
    }

    [HttpGet("{id:int}/file")]
    [Authorize(Policy = "WearView")]
    public async Task<IActionResult> GetFile(int id)
    {
        var image = await db
            .WearImages.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new { i.Data, i.ContentType })
            .FirstOrDefaultAsync();
        return image == null ? NotFound(new { message = "Wear image not found" }) : File(image.Data, image.ContentType);
    }

    // "Upload". Infosys refused a file name that was already uploaded ("Der findes allerede en fil med samme navn").
    [HttpPost]
    [Authorize(Policy = "WearManagement")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<WearImageDto>> Create([FromBody] CreateWearImageDto dto)
    {
        var fileName = FileNameOf(dto.FileName);
        if (fileName.Length == 0 || fileName.Length > WearImage.MaxFileNameLength)
            return BadRequest(new { message = $"The file name must be 1 to {WearImage.MaxFileNameLength} characters" });

        var data = ImageFiles.FromDataUrl(dto.Data);
        var info = data == null ? null : ImageFiles.Inspect(data);
        if (info == null)
            return BadRequest(NotImage);

        if (await db.WearImages.AnyAsync(i => i.FileName == fileName))
            return Conflict(NameTaken);

        var image = new WearImage
        {
            FileName = fileName,
            ContentType = info.ContentType,
            Data = data!,
        };
        db.WearImages.Add(image);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The same name was uploaded at the same time (the file name is unique)
            return Conflict(NameTaken);
        }

        logger.LogInformation("Wear image {WearImageId} uploaded by user {UserId}", image.Id, CurrentUserId());

        return Ok(new WearImageDto(image.Id, image.FileName));
    }

    // Infosys basename(): only the name, without any folder the browser sent
    public static string FileNameOf(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var slash = name.LastIndexOfAny(['/', '\\']);
        return name[(slash + 1)..];
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
