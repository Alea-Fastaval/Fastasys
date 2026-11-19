using System.Text.Json.Serialization;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Services;

[JsonConverter(typeof(JsonStringEnumConverter<KarmaInputSource>))]
public enum KarmaInputSource { Signup, Assignment }

[JsonConverter(typeof(JsonStringEnumConverter<KarmaExclusionReason>))]
public enum KarmaExclusionReason { NonTimeExclusive, UnrankedPlayerSignup, GameMasterAssignment, UnknownSignupRole, CancelledSchedule }

public record KarmaExcludedInput(
    KarmaInputSource Source,
    int ScheduleId,
    int ActivityId,
    string ActivityTitle,
    int? RunId,
    string Role,
    int? Priority,
    KarmaExclusionReason Reason
);

public record KarmaCalculation(KarmaResult Karma, IReadOnlyList<KarmaExcludedInput> ExcludedInputs);

public record ParticipantKarmaDto(int ParticipantId, string ParticipantName, KarmaResult Karma, IReadOnlyList<KarmaExcludedInput> ExcludedInputs);

/// <summary>Loads current source data in two queries per batch; totals are never stored or cached.</summary>
public sealed class KarmaService
{
    private readonly InfosysDbContext _db;

    public KarmaService(InfosysDbContext db) => _db = db;

    public async Task<ParticipantKarmaDto?> GetParticipantAsync(int participantId, CancellationToken ct = default)
    {
        var participant = await _db.Participants.AsNoTracking().Where(p => p.Id == participantId)
            .Select(p => new { p.FirstName, p.LastName }).SingleOrDefaultAsync(ct);
        if (participant == null) return null;

        var calculation = (await CalculateAsync([participantId], ct))[participantId];
        return new ParticipantKarmaDto(participantId, $"{participant.FirstName} {participant.LastName}",
            calculation.Karma, calculation.ExcludedInputs);
    }

    public async Task<IReadOnlyDictionary<int, KarmaCalculation>> CalculateAsync(IEnumerable<int> participantIds, CancellationToken ct = default)
    {
        var ids = participantIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, KarmaCalculation>();

        var signups = await _db.ParticipantSchedules.AsNoTracking().Where(s => ids.Contains(s.ParticipantId))
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.ParticipantId,
                s.ScheduleId,
                s.Schedule.ActivityId,
                s.Schedule.Activity.Title,
                s.Role,
                s.Priority,
                s.Schedule.IsCancelled,
                s.Schedule.Activity.IsTimeExclusive
            }).ToListAsync(ct);
        var assignments = await _db.RunSeats.AsNoTracking().Where(s => ids.Contains(s.ParticipantId))
            .OrderBy(s => s.RunId).ThenBy(s => s.SeatNumber)
            .Select(s => new
            {
                s.ParticipantId,
                s.RunId,
                s.Run.ScheduleId,
                s.Run.Schedule.ActivityId,
                s.Run.Schedule.Activity.Title,
                s.Role,
                s.Run.Schedule.IsCancelled,
                s.Run.Schedule.Activity.KarmaType,
                s.Run.Schedule.Activity.IsTimeExclusive
            }).ToListAsync(ct);

        var signupsByParticipant = signups.ToLookup(s => s.ParticipantId);
        var assignmentsByParticipant = assignments.ToLookup(s => s.ParticipantId);
        var results = new Dictionary<int, KarmaCalculation>();
        foreach (var id in ids)
        {
            var ranked = new List<KarmaSignup>();
            var seats = new List<KarmaAssignment>();
            var excluded = new List<KarmaExcludedInput>();
            foreach (var signup in signupsByParticipant[id])
            {
                // Do not use Enum.TryParse: numeric strings and unknown legacy text must not become a role.
                RunSeatRole? role = signup.Role switch
                {
                    "Player" => RunSeatRole.Player,
                    "GameMaster" => RunSeatRole.GameMaster,
                    _ => null
                };
                KarmaExclusionReason? reason = signup.IsCancelled ? KarmaExclusionReason.CancelledSchedule
                    : !signup.IsTimeExclusive ? KarmaExclusionReason.NonTimeExclusive
                    : role == null ? KarmaExclusionReason.UnknownSignupRole
                    : role == RunSeatRole.Player && signup.Priority == null ? KarmaExclusionReason.UnrankedPlayerSignup
                    : null;
                if (reason != null)
                    excluded.Add(new(KarmaInputSource.Signup, signup.ScheduleId, signup.ActivityId, signup.Title,
                        null, signup.Role, signup.Priority, reason.Value));
                else
                    ranked.Add(new(signup.Priority, role!.Value, signup.IsTimeExclusive));
            }
            foreach (var assignment in assignmentsByParticipant[id])
            {
                KarmaExclusionReason? reason = assignment.IsCancelled ? KarmaExclusionReason.CancelledSchedule
                    : !assignment.IsTimeExclusive ? KarmaExclusionReason.NonTimeExclusive
                    : assignment.Role == RunSeatRole.GameMaster ? KarmaExclusionReason.GameMasterAssignment
                    : null;
                if (reason != null)
                    excluded.Add(new(KarmaInputSource.Assignment, assignment.ScheduleId, assignment.ActivityId, assignment.Title,
                        assignment.RunId, assignment.Role.ToString(), null, reason.Value));
                else
                    seats.Add(new(assignment.KarmaType, assignment.Role, assignment.IsTimeExclusive));
            }

            var result = KarmaRules.Calculate(ranked, seats);
            // Counts and explanations share one classification, including legacy roles and cancelled inputs.
            int Count(KarmaInputSource source, KarmaExclusionReason reason) => excluded.Count(e => e.Source == source && e.Reason == reason);
            result = result with
            {
                Exclusions = new KarmaExclusions(
                    Count(KarmaInputSource.Signup, KarmaExclusionReason.NonTimeExclusive),
                    Count(KarmaInputSource.Signup, KarmaExclusionReason.UnrankedPlayerSignup),
                    Count(KarmaInputSource.Assignment, KarmaExclusionReason.NonTimeExclusive),
                    Count(KarmaInputSource.Assignment, KarmaExclusionReason.GameMasterAssignment),
                    Count(KarmaInputSource.Signup, KarmaExclusionReason.UnknownSignupRole),
                    Count(KarmaInputSource.Signup, KarmaExclusionReason.CancelledSchedule),
                    Count(KarmaInputSource.Assignment, KarmaExclusionReason.CancelledSchedule))
            };
            results[id] = new KarmaCalculation(result, excluded.AsReadOnly());
        }
        return results;
    }
}
