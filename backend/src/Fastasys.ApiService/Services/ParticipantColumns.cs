using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>A column that can be added to the participant list.</summary>
/// <param name="Value">The value for a participant with category, country, organizer area and game loaded</param>
/// <param name="EditorsOnly">
/// Notes are hidden from users who may only view participants, as on the participant page. Infosys let its
/// Read-only role add them to the list (missing access check, fixed).
/// </param>
public sealed record ParticipantColumn(Func<Participant, object?> Value, bool EditorsOnly = false);

/// <summary>
/// The columns of the Infosys participant list ("Tilføj/fjern kolonne", ParticipantModel::getDisplayColumns):
/// the columns with a readable name, minus the ones Infosys filters out and the removed ones, plus the signup
/// notes. Columns for features not ported yet (sleeping rooms, hero force) come with those features.
/// </summary>
public static class ParticipantColumns
{
    public static readonly IReadOnlyDictionary<string, ParticipantColumn> All = new Dictionary<string, ParticipantColumn>
    {
        ["firstName"] = new(p => p.FirstName),
        ["lastName"] = new(p => p.LastName),
        ["email"] = new(p => p.Email),
        ["phoneNumber"] = new(p => p.PhoneNumber),
        ["addressLine1"] = new(p => p.AddressLine1),
        ["zipCode"] = new(p => p.ZipCode),
        ["city"] = new(p => p.City),
        ["smsSignup"] = new(p => p.SmsSignup),
        ["activityLanguages"] = new(p => p.ActivityLanguages),
        ["category"] = new(p => p.Category?.Name),
        ["isAuthorOrDesigner"] = new(p => p.IsAuthorOrDesigner),
        ["amountPaid"] = new(p => p.AmountPaid),
        ["createdAt"] = new(p => Utc(p.CreatedAt)),
        ["organizerArea"] = new(p => p.OrganizerArea?.NameDanish),
        ["game"] = new(p => p.GameActivity?.Title),
        ["isNoShow"] = new(p => p.IsNoShow),
        ["youthClub"] = new(p => p.YouthClub),
        ["setupHelpMonday"] = new(p => p.SetupHelpMonday),
        ["setupHelpTuesday"] = new(p => p.SetupHelpTuesday),
        ["checkedInAt"] = new(p => p.CheckedInAt == null ? null : Utc(p.CheckedInAt.Value)),
        ["desiredActivities"] = new(p => p.DesiredActivities),
        ["birthDate"] = new(p => p.BirthDate),
        ["updatedAt"] = new(p => Utc(p.UpdatedAt)),
        ["signedUpAt"] = new(p => Utc(p.SignedUpAt)),
        ["isAnnulled"] = new(p => p.IsAnnulled),
        ["desiredHeroTasks"] = new(p => p.DesiredHeroTasks),
        ["nickname"] = new(p => p.Nickname),
        ["financialSupport"] = new(p => p.FinancialSupport),
        ["preferredLanguage"] = new(p => p.PreferredLanguage),
        ["country"] = new(p => p.Country?.NameDanish),
        ["superGameMaster"] = new(p => p.SuperGameMaster),
        ["maxGameMasterRuns"] = new(p => p.MaxGameMasterRuns),
        ["superRulesGuide"] = new(p => p.SuperRulesGuide),
        ["maxRulesGuideGames"] = new(p => p.MaxRulesGuideGames),
        ["pronouns"] = new(p => p.Pronouns),
        ["sleepingAreas"] = new(p => p.SleepingAreas),
        ["adminNote"] = new(p => p.AdminNote, EditorsOnly: true),
        ["messageToParticipant"] = new(p => p.MessageToParticipant, EditorsOnly: true),
        ["paymentNote"] = new(p => p.PaymentNote, EditorsOnly: true),
        ["commentNote"] = new(p => p.CommentNote, EditorsOnly: true),
        ["heroForceNote"] = new(p => p.HeroForceNote, EditorsOnly: true),
        ["juniorContact"] = new(p => p.JuniorContact, EditorsOnly: true),
        ["juniorComment"] = new(p => p.JuniorComment, EditorsOnly: true),
        ["sleepingNote"] = new(p => p.SleepingNote, EditorsOnly: true),
    };

    // Times are stored in UTC; marking them so makes them go out with a "Z"
    private static DateTime Utc(DateTime time) => DateTime.SpecifyKind(time, DateTimeKind.Utc);
}
