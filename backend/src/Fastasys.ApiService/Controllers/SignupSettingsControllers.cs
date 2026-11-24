using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fastasys.ApiService.Controllers;

// Whether the signup is open, with the dates behind it. Dates are Danish calendar days; JuniorSignupStart is a UTC
// instant. Infosys sent the same dates as live values in api/signup/config/main (signup_end, paybyday, con_start,
// junior_signup_open); the open/closed state is new, because it used to be a setting in the WordPress plugin.
public record SignupStatusDto(
    bool IsSignupOpen,
    bool IsLateSignup,
    bool IsJuniorSignupOpen,
    DateOnly? SignupStart,
    DateOnly? SignupEnd,
    DateTime? JuniorSignupStart,
    DateOnly? ConventionStart,
    DateOnly? ConventionEnd,
    DateOnly? PayByDay
);

// IsOpen is the manual switch; Status is the result of the switch and the dates
public record SignupSettingsDto(bool IsOpen, SignupStatusDto Status);

public record UpdateSignupSettingsDto
{
    public required bool IsOpen { get; init; }
}

/// <summary>
/// The signup's manual switch (agreed on 2026-11-24: a person makes the final decision to open the signup).
/// </summary>
[ApiController]
[Route("api/management/signup-settings")]
[Authorize(Policy = "SignupManagement")]
public class SignupSettingsController(
    InfosysDbContext db,
    IOptions<ConventionOptions> convention,
    TimeProvider clock,
    ILogger<SignupSettingsController> logger
) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SignupSettingsDto>> Get(CancellationToken ct)
    {
        var isOpen = await IsSwitchedOnAsync(db, ct);
        return Ok(ToDto(isOpen));
    }

    [HttpPut]
    public async Task<ActionResult<SignupSettingsDto>> Update(UpdateSignupSettingsDto values, CancellationToken ct)
    {
        var settings = await db.SignupSettings.SingleOrDefaultAsync(
            settings => settings.Id == SignupSettings.SingletonId,
            ct
        );
        if (settings == null)
        {
            settings = new SignupSettings();
            db.SignupSettings.Add(settings);
        }
        settings.IsOpen = values.IsOpen;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Signup switch set to {IsOpen} by user {UserId}",
            settings.IsOpen,
            User.FindFirstValue(ClaimTypes.NameIdentifier)
        );
        return Ok(ToDto(settings.IsOpen));
    }

    private SignupSettingsDto ToDto(bool isOpen) =>
        new(isOpen, ToStatusDto(isOpen, convention.Value, clock.GetUtcNow().UtcDateTime));

    // The switch; off while the row is missing
    public static Task<bool> IsSwitchedOnAsync(InfosysDbContext db, CancellationToken ct) =>
        db
            .SignupSettings.AsNoTracking()
            .Where(settings => settings.Id == SignupSettings.SingletonId)
            .Select(settings => settings.IsOpen)
            .SingleOrDefaultAsync(ct);

    public static SignupStatusDto ToStatusDto(bool isSwitchedOn, ConventionOptions convention, DateTime nowUtc)
    {
        var isOpen = SignupOpeningRules.IsOpen(isSwitchedOn, convention.SignupStart, nowUtc);
        return new SignupStatusDto(
            isOpen,
            SignupOpeningRules.IsLate(convention.SignupEnd, nowUtc),
            SignupOpeningRules.IsJuniorOpen(isOpen, convention.JuniorSignupStart, nowUtc),
            convention.SignupStart,
            convention.SignupEnd,
            convention.JuniorSignupStart is DateTime junior ? SignupOpeningRules.CopenhagenToUtc(junior) : null,
            convention.Start,
            convention.End,
            convention.PayByDay
        );
    }
}

/// <summary>
/// The signup's state for the public signup, so anyone may read it.
/// </summary>
[ApiController]
[Route("api/signup/status")]
[AllowAnonymous]
public class SignupStatusController(InfosysDbContext db, IOptions<ConventionOptions> convention, TimeProvider clock)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SignupStatusDto>> Get(CancellationToken ct)
    {
        var isSwitchedOn = await SignupSettingsController.IsSwitchedOnAsync(db, ct);
        return Ok(SignupSettingsController.ToStatusDto(isSwitchedOn, convention.Value, clock.GetUtcNow().UtcDateTime));
    }
}
