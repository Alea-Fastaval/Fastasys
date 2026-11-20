using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record SignupSubmissionDto(string Email, string FormDataJson);

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class SignupController(InfosysDbContext db) : ControllerBase
{
    [HttpGet("pages")]
    public async Task<IActionResult> GetPages()
    {
        var pages = await db
            .SignupPages.Include(p => p.Elements.OrderBy(e => e.OrderIndex))
            .Where(p => p.IsActive)
            .OrderBy(p => p.OrderIndex)
            .AsNoTracking()
            .ToListAsync();
        return Ok(pages);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SignupSubmissionDto dto)
    {
        var submission = new SignupSubmission
        {
            Email = dto.Email,
            FormDataJson = dto.FormDataJson,
            SubmittedAt = DateTime.UtcNow,
            IsConfirmed = false,
            ConfirmationToken = Guid.NewGuid().ToString("N"),
        };

        db.SignupSubmissions.Add(submission);
        await db.SaveChangesAsync();

        return Ok(
            new
            {
                submissionId = submission.Id,
                token = submission.ConfirmationToken,
                message = "Signup submitted successfully. Confirmation pending.",
            }
        );
    }
}
