using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record CreateRoomDto(string Name, string Location, int Capacity, string Description);

[ApiController]
[Route("api/[controller]")]
public class RoomsController(InfosysDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRooms()
    {
        var rooms = await db.Rooms.AsNoTracking().ToListAsync();
        return Ok(rooms);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var room = new Room
        {
            Name = dto.Name,
            Location = dto.Location,
            Capacity = dto.Capacity,
            Description = dto.Description,
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return Ok(room);
    }
}
