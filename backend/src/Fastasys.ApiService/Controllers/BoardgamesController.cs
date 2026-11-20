using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record CheckoutBoardgameDto(int ParticipantId);

[ApiController]
[Route("api/[controller]")]
public class BoardgamesController(InfosysDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var query = db.Boardgames.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(b =>
                b.Title.Contains(search) || b.Author.Contains(search) || b.Barcode.Contains(search)
            );
        }
        var games = await query.ToListAsync();
        return Ok(games);
    }

    [HttpGet("loans")]
    public async Task<IActionResult> GetLoans()
    {
        var loans = await db
            .BoardgameLoans.Include(l => l.Boardgame)
            .Include(l => l.Participant)
            .OrderByDescending(l => l.LoanedAt)
            .AsNoTracking()
            .Select(l => new
            {
                l.Id,
                l.BoardgameId,
                BoardgameTitle = l.Boardgame.Title,
                l.ParticipantId,
                ParticipantName = l.Participant.DisplayName(),
                l.LoanedAt,
                l.ReturnedAt,
            })
            .ToListAsync();

        return Ok(loans);
    }

    [HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, [FromBody] CheckoutBoardgameDto dto)
    {
        var game = await db.Boardgames.FindAsync(id);
        if (game == null)
            return NotFound(new { message = "Boardgame not found" });

        if (!game.IsPresent)
            return BadRequest(new { message = "Boardgame is currently on loan" });

        var participant = await db.Participants.FindAsync(dto.ParticipantId);
        if (participant == null)
            return NotFound(new { message = "Participant not found" });

        game.IsPresent = false;
        var loan = new BoardgameLoan
        {
            BoardgameId = id,
            ParticipantId = dto.ParticipantId,
            LoanedAt = DateTime.UtcNow,
        };

        db.BoardgameLoans.Add(loan);
        await db.SaveChangesAsync();

        return Ok(new { message = "Boardgame checked out successfully", loanId = loan.Id });
    }

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id)
    {
        var game = await db.Boardgames.FindAsync(id);
        if (game == null)
            return NotFound(new { message = "Boardgame not found" });

        var activeLoan = await db
            .BoardgameLoans.Where(l => l.BoardgameId == id && l.ReturnedAt == null)
            .FirstOrDefaultAsync();

        game.IsPresent = true;
        if (activeLoan != null)
        {
            activeLoan.ReturnedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Boardgame returned successfully" });
    }
}
