using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fastasys.ApiService.Controllers;

[ApiController]
[Route("api/participants/{participantId:int}/karma")]
[Authorize(Policy = "ActivityView")]
public class KarmaController : ControllerBase
{
    private readonly KarmaService _karma;

    public KarmaController(KarmaService karma) => _karma = karma;

    [HttpGet]
    public async Task<ActionResult<ParticipantKarmaDto>> Get(int participantId, CancellationToken ct)
    {
        var result = await _karma.GetParticipantAsync(participantId, ct);
        return result == null ? NotFound(new { message = "Participant not found" }) : Ok(result);
    }
}
