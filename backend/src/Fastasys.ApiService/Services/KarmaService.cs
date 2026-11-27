using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Services;

public sealed class KarmaService(InfosysDbContext db)
{
    public async Task<IReadOnlyDictionary<int, int>> CalculateAsync(
        IEnumerable<int> participantIds,
        CancellationToken ct = default
    )
    {
        var ids = participantIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<int, int>();

        var settings = await db
            .ActivitySettings.AsNoTracking()
            .SingleOrDefaultAsync(settings => settings.Id == ActivitySettings.SingletonId, ct)
            ?? new ActivitySettings();
        var signups = await db
            .ScheduleSignups.AsNoTracking()
            .Where(signup =>
                ids.Contains(signup.ParticipantId)
                && signup.Schedule.CancelledAt == null
                && signup.Schedule.Activity.IsTimeExclusive
            )
            .Select(signup => new
            {
                signup.ParticipantId,
                signup.Priority,
                signup.Role,
            })
            .ToListAsync(ct);
        var assignments = await db
            .RunSeats.AsNoTracking()
            .Where(seat =>
                ids.Contains(seat.ParticipantId)
                && seat.Run.Schedule.CancelledAt == null
                && seat.Run.Schedule.Activity.IsTimeExclusive
                && seat.Role != RunSeatRole.GameMaster
            )
            .Select(seat => new
            {
                seat.ParticipantId,
                seat.Role,
                seat.Run.Schedule.Activity.KarmaType,
            })
            .ToListAsync(ct);

        var signupsByParticipant = signups.ToLookup(signup => signup.ParticipantId);
        var assignmentsByParticipant = assignments.ToLookup(seat => seat.ParticipantId);
        return ids.ToDictionary(
            id => id,
            id =>
                KarmaRules.Calculate(
                    signupsByParticipant[id].Select(signup => new KarmaSignup(signup.Priority, signup.Role, true)),
                    assignmentsByParticipant[id].Select(seat => new KarmaAssignment(seat.KarmaType, seat.Role, true)),
                    settings
                )
        );
    }
}
