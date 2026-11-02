using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record ParticipantDto(
    int Id,
    string FirstName,
    string LastName,
    string Nickname,
    string Email,
    string PhoneNumber,
    DateTime BirthDate,
    string Address,
    string ZipCode,
    string City,
    string Country,
    bool IsCheckedIn,
    DateTime? CheckedInAt,
    string Barcode,
    DateTime CreatedAt
);

public record CreateParticipantDto(
    string FirstName,
    string LastName,
    string Nickname,
    string Email,
    string PhoneNumber,
    DateTime BirthDate,
    string Address,
    string ZipCode,
    string City,
    string Country,
    string MedicalInfo
);

public record UpdateParticipantDto(
    string FirstName,
    string LastName,
    string Nickname,
    string Email,
    string PhoneNumber,
    DateTime BirthDate,
    string Address,
    string ZipCode,
    string City,
    string Country,
    string MedicalInfo
);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ParticipantsController : ControllerBase
{
    private readonly InfosysDbContext _db;

    public ParticipantsController(InfosysDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParticipantDto>>> GetAll([FromQuery] string? search)
    {
        var query = _db.Participants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.FirstName.Contains(search) ||
                p.LastName.Contains(search) ||
                p.Nickname.Contains(search) ||
                p.Email.Contains(search) ||
                p.Barcode.Contains(search));
        }

        var list = await query
            .OrderByDescending(p => p.Id)
            .Take(100)
            .Select(p => ToDto(p))
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ParticipantDto>> Get(int id)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound();
        return Ok(ToDto(participant));
    }

    [HttpPost]
    [Authorize(Policy = "ParticipantManagement")]
    public async Task<ActionResult<ParticipantDto>> Create([FromBody] CreateParticipantDto dto)
    {
        var barcode = "EAN" + Random.Shared.Next(10000000, 99999999).ToString();
        var entity = new Participant
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Nickname = dto.Nickname,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            BirthDate = dto.BirthDate,
            Address = dto.Address,
            ZipCode = dto.ZipCode,
            City = dto.City,
            Country = dto.Country,
            MedicalInfo = dto.MedicalInfo,
            Barcode = barcode,
            CreatedAt = DateTime.UtcNow
        };

        _db.Participants.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, ToDto(entity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ParticipantManagement")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateParticipantDto dto)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound();

        participant.FirstName = dto.FirstName;
        participant.LastName = dto.LastName;
        participant.Nickname = dto.Nickname;
        participant.Email = dto.Email;
        participant.PhoneNumber = dto.PhoneNumber;
        participant.BirthDate = dto.BirthDate;
        participant.Address = dto.Address;
        participant.ZipCode = dto.ZipCode;
        participant.City = dto.City;
        participant.Country = dto.Country;
        participant.MedicalInfo = dto.MedicalInfo;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/checkin")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound();

        participant.IsCheckedIn = true;
        participant.CheckedInAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Participant checked in successfully", checkedInAt = participant.CheckedInAt });
    }

    [HttpGet("{id:int}/barcode")]
    public async Task<IActionResult> GetBarcode(int id)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound();

        return Ok(new { id = participant.Id, barcode = participant.Barcode });
    }

    [HttpGet("{id:int}/schedule")]
    public async Task<IActionResult> GetSchedule(int id)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound();

        var activitySchedules = await _db.ParticipantSchedules
            .Include(ps => ps.Schedule)
                .ThenInclude(s => s.Activity)
            .Where(ps => ps.ParticipantId == id)
            .Select(ps => new
            {
                type = "Activity",
                title = ps.Schedule.Activity.Title,
                startTime = ps.Schedule.StartTime,
                endTime = ps.Schedule.EndTime,
                role = ps.Role
            })
            .ToListAsync();

        var heroForceShifts = await _db.HeroForceShiftParticipants
            .Include(gsp => gsp.Shift)
            .Where(gsp => gsp.ParticipantId == id)
            .Select(gsp => new
            {
                type = "Hero Force Shift",
                title = gsp.Shift.Title,
                startTime = gsp.Shift.StartTime,
                endTime = gsp.Shift.EndTime,
                role = "Worker"
            })
            .ToListAsync();

        var fullSchedule = activitySchedules.Concat(heroForceShifts).OrderBy(s => s.startTime);

        return Ok(new { participantId = id, schedule = fullSchedule });
    }

    private static ParticipantDto ToDto(Participant p) => new(
        p.Id,
        p.FirstName,
        p.LastName,
        p.Nickname,
        p.Email,
        p.PhoneNumber,
        p.BirthDate,
        p.Address,
        p.ZipCode,
        p.City,
        p.Country,
        p.IsCheckedIn,
        p.CheckedInAt,
        p.Barcode,
        p.CreatedAt
    );
}
