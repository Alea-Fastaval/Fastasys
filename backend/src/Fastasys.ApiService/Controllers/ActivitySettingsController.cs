using System.ComponentModel.DataAnnotations;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record ActivitySettingsDto
{
    [Range(0, 1000)]
    public required int SmallKarma { get; init; }

    [Range(0, 1000)]
    public required int MediumKarma { get; init; }

    [Range(0, 1000)]
    public required int BigKarma { get; init; }
}

[ApiController]
[Route("api/management/activity-settings")]
[Authorize(Policy = "ActivityManagement")]
public class ActivitySettingsController(InfosysDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ActivitySettingsDto>> Get(CancellationToken ct)
    {
        var settings = await db
            .ActivitySettings.AsNoTracking()
            .SingleOrDefaultAsync(settings => settings.Id == ActivitySettings.SingletonId, ct)
            ?? new ActivitySettings();
        return Ok(ToDto(settings));
    }

    [HttpPut]
    public async Task<ActionResult<ActivitySettingsDto>> Update(ActivitySettingsDto values, CancellationToken ct)
    {
        var settings = await db.ActivitySettings.SingleOrDefaultAsync(
            settings => settings.Id == ActivitySettings.SingletonId,
            ct
        );
        if (settings == null)
        {
            settings = new ActivitySettings();
            db.ActivitySettings.Add(settings);
        }
        settings.SmallKarma = values.SmallKarma;
        settings.MediumKarma = values.MediumKarma;
        settings.BigKarma = values.BigKarma;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(settings));
    }

    private static ActivitySettingsDto ToDto(ActivitySettings settings) =>
        new()
        {
            SmallKarma = settings.SmallKarma,
            MediumKarma = settings.MediumKarma,
            BigKarma = settings.BigKarma,
        };
}
