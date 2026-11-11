using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>A room booked by a run during its schedule.</summary>
public readonly record struct RoomBooking(int RoomId, DateTime Start, DateTime End, bool IsRoomExclusive);

/// <summary>
/// Business rules for runs (Infosys: hold), their seats (pladser) and the rooms they use (lokaler).
/// </summary>
public static class RunRules
{
    // Infosys Hold::needsGMs: fewer game masters than the activity's spilledere_per_hold
    public static bool NeedsGameMasters(int gameMasters, int gameMastersPerTeam) => gameMasters < gameMastersPerTeam;

    // Infosys Hold::needsGamers: fewer players than the activity's min_deltagere_per_hold
    public static bool NeedsPlayers(int players, int minPlayersPerTeam) => players < minPlayersPerTeam;

    // Infosys Hold::canUseGamers: fewer players than the activity's max_deltagere_per_hold
    public static bool CanUsePlayers(int players, int maxPlayersPerTeam) => players < maxPlayersPerTeam;

    // Infosys Pladser::setDeltagerPlads: a player can only be added while the run can use players,
    // a game master only while the run still needs game masters.
    public static bool HasRoomFor(RunSeatRole role, int players, int gameMasters, Activity activity) =>
        role == RunSeatRole.Player
            ? CanUsePlayers(players, activity.MaxPlayersPerTeam)
            : NeedsGameMasters(gameMasters, activity.GameMastersPerTeam);

    // Infosys Hold::getNextHoldnummer: MAX(holdnummer) + 1 within the schedule, so gaps are never reused
    public static int NextRunNumber(IEnumerable<int> numbersInSchedule) =>
        numbersInSchedule.DefaultIfEmpty(0).Max() + 1;

    // Infosys Pladser::getNextPladsnummer: the first free number counting from 1, so gaps are reused
    public static int NextSeatNumber(IEnumerable<int> seatNumbersInRun)
    {
        var counter = 1;
        foreach (var number in seatNumbersInRun.Order())
        {
            if (number != counter) return counter;
            counter++;
        }

        return counter;
    }

    // Infosys Lokaler::isOccupiedBetween. A booking only blocks the room when it overlaps the period and
    // either the new activity or the booked one fills the room (lokale_eksklusiv). Infosys also treats rooms
    // marked "kan_bookes = nej" as occupied; Fastasys rooms have no such flag yet.
    public static bool IsRoomOccupied(int roomId, DateTime start, DateTime end, bool isRoomExclusive, IEnumerable<RoomBooking> bookings)
    {
        foreach (var booking in bookings)
        {
            if (booking.RoomId != roomId) continue;

            var overlaps = (booking.Start >= start && booking.Start < end)
                || (booking.End > start && booking.End <= end)
                || (booking.Start < start && booking.End > end);

            if (overlaps && (isRoomExclusive || booking.IsRoomExclusive)) return true;
        }

        return false;
    }
}
