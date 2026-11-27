using System.Runtime.CompilerServices;
using System.Security.Claims;
using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fastasys.ApiService.Data;

// Logs every saved change to a participant's AmountPaid ("Forudbetalt"), whichever code made it, with the old and the
// new amount and the logged-in user. Asked for in a PR review and agreed on 2026-11-21; Infosys logged only some of the
// changes, mostly without the amounts. Logging the amounts is an agreed exception to logging only technical IDs.
// - The changes are read before the save and logged after it succeeded, so a failed save logs nothing.
// - A new participant is logged only when they start with an amount other than 0.
// - A participant attached as modified without being loaded first (DbSet.Update) has no old amount to compare with and
//   is not logged; Fastasys always loads the participant first.
// - Bulk updates (ExecuteUpdate, raw SQL) bypass the change tracker and are not logged. Fastasys has none for AmountPaid.
public class AmountPaidLogInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<AmountPaidLogInterceptor> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // The changes of each save in progress, by context
    private readonly ConditionalWeakTable<DbContext, List<AmountPaidChange>> _pending = new();

    public AmountPaidLogInterceptor(ILogger<AmountPaidLogInterceptor> logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Log(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Log(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Discard(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        Discard(eventData.Context);
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context == null) return;

        var changes = new List<AmountPaidChange>();
        foreach (var entry in context.ChangeTracker.Entries<Participant>())
        {
            var amount = entry.Property(p => p.AmountPaid);
            if (entry.State == EntityState.Added && amount.CurrentValue != 0)
            {
                changes.Add(new AmountPaidChange(entry, null, amount.CurrentValue));
            }
            else if (entry.State == EntityState.Modified && amount.IsModified && amount.OriginalValue != amount.CurrentValue)
            {
                changes.Add(new AmountPaidChange(entry, amount.OriginalValue, amount.CurrentValue));
            }
        }
        _pending.AddOrUpdate(context, changes);
    }

    private void Log(DbContext? context)
    {
        if (context == null || !_pending.TryGetValue(context, out var changes)) return;
        _pending.Remove(context);

        var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        foreach (var change in changes)
        {
            // Read after the save, so a generated id would be known
            var participantId = change.Entry.Entity.Id;
            if (change.OldAmount is int oldAmount)
            {
                _logger.LogInformation(
                    "Participant {ParticipantId}'s amount paid changed from {OldAmountPaid} to {NewAmountPaid} by user {UserId}",
                    participantId, oldAmount, change.NewAmount, userId);
            }
            else
            {
                _logger.LogInformation(
                    "Participant {ParticipantId} created with amount paid {AmountPaid} by user {UserId}",
                    participantId, change.NewAmount, userId);
            }
        }
    }

    private void Discard(DbContext? context)
    {
        if (context != null) _pending.Remove(context);
    }

    // OldAmount is null for a new participant
    private sealed record AmountPaidChange(EntityEntry<Participant> Entry, int? OldAmount, int NewAmount);
}
