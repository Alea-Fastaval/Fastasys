using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// The upload page's two images as data URLs ("data:image/png;base64,..."): the image as it was chosen, and the cut-out
// for the ID card
public record UpdateParticipantPhotoDto(string? Original, string? Cropped);

// Infosys PhotoController::storeOriginal and storeCropped, and the photo files in its public uploads folder. Here only
// staff upload, from the participant page (the public upload link for participants comes later, agreed 2026-11-18),
// and the photos are personal data behind login: the right to edit participants, like the rest of the ID card.
[ApiController]
[Route("api/participants/{participantId:int}/photo")]
[Authorize(Policy = "ParticipantManagement")]
public class ParticipantPhotoController : ControllerBase
{
    private readonly InfosysDbContext _db;
    private readonly ILogger<ParticipantPhotoController> _logger;

    public ParticipantPhotoController(InfosysDbContext db, ILogger<ParticipantPhotoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // The cut-out used on the ID card
    [HttpGet("cropped")]
    public async Task<IActionResult> GetCropped(int participantId)
    {
        var photo = await _db.ParticipantPhotos
            .AsNoTracking()
            .Where(p => p.ParticipantId == participantId)
            .Select(p => new { p.Cropped, p.CroppedContentType })
            .FirstOrDefaultAsync();
        return photo == null ? NotFound(new { message = "No photo" }) : File(photo.Cropped, photo.CroppedContentType);
    }

    // The image as it was chosen, so the upload page can crop it again (Infosys PhotoModel::getExistingImage)
    [HttpGet("original")]
    public async Task<IActionResult> GetOriginal(int participantId)
    {
        var photo = await _db.ParticipantPhotos
            .AsNoTracking()
            .Where(p => p.ParticipantId == participantId)
            .Select(p => new { p.Original, p.OriginalContentType })
            .FirstOrDefaultAsync();
        return photo == null ? NotFound(new { message = "No photo" }) : File(photo.Original, photo.OriginalContentType);
    }

    // "Upload": replaces the participant's photo. Infosys sent the two images as two requests and accepted whatever the
    // browser said they were; here both come together, and each must really be a PNG, JPEG or GIF of at most 10 MB.
    [HttpPut]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantPhotoDto dto)
    {
        var original = ImageFiles.FromDataUrl(dto.Original);
        var cropped = ImageFiles.FromDataUrl(dto.Cropped);
        var originalInfo = original == null ? null : ImageFiles.Inspect(original);
        var croppedInfo = cropped == null ? null : ImageFiles.Inspect(cropped);
        if (originalInfo == null || croppedInfo == null)
        {
            return BadRequest(new { message = "The photo must be a PNG, JPEG or GIF image of at most 10 MB" });
        }

        if (!await _db.Participants.AnyAsync(p => p.Id == participantId)) return NotFound(new { message = "Participant not found" });

        var photo = await _db.ParticipantPhotos.FindAsync(participantId);
        if (photo == null)
        {
            photo = new ParticipantPhoto { ParticipantId = participantId };
            _db.ParticipantPhotos.Add(photo);
        }

        photo.Original = original!;
        photo.OriginalContentType = originalInfo.ContentType;
        photo.Cropped = cropped!;
        photo.CroppedContentType = croppedInfo.ContentType;
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Infosys: "{user} uploadede tilpasset billede for deltager #{id}"
        _logger.LogInformation("Participant {ParticipantId}'s photo uploaded by user {UserId}", participantId, CurrentUserId());
        return NoContent();
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
