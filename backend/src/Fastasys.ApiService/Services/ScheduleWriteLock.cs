using Fastasys.ApiService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fastasys.ApiService.Services;

/// <summary>Serializes cancellation and new preferences/runs/seats for the same scheduled occurrence.</summary>
public static class ScheduleWriteLock
{
    // Room allocation and restoration use the same ordered locks before checking active bookings.
    public static Task AcquireRoomsAsync(InfosysDbContext db) => db.Database.IsNpgsql()
        ? db.Database.ExecuteSqlRawAsync("SELECT \"Id\" FROM \"Rooms\" ORDER BY \"Id\" FOR UPDATE")
        : Task.CompletedTask;

    public static async Task<IDbContextTransaction?> AcquireAsync(InfosysDbContext db, int scheduleId)
    {
        // Unit tests use EF's in-memory provider; deployed data uses PostgreSQL.
        if (!db.Database.IsNpgsql()) return null;
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM schedule WHERE \"Id\" = {scheduleId} FOR UPDATE");
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
