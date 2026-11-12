using Microsoft.Data.SqlClient;

namespace HRMS.Next.Modules.Leave;

public sealed class LeaveBusyException(Exception inner) : Exception(
    "درخواست هم‌زمان دیگری در حال پردازش است؛ چند لحظه بعد دوباره تلاش کنید.", inner);

internal static class LeaveTransaction
{
    // A 1205 victim is rolled back by SQL Server. Retry the whole operation only after
    // its transaction has been disposed, discarding every rolled-back tracked value.
    // Never retry validation, optimistic concurrency, timeouts or ambiguous commits.
    public static async Task<T> RunAsync<T>(LeaveDbContext db, Func<Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await operation(); }
            catch (Exception error) when (IsDeadlock(error))
            {
                db.ChangeTracker.Clear();
                if (attempt >= 3) throw new LeaveBusyException(error);
                await Task.Delay(Random.Shared.Next(30, 100) * attempt, ct);
            }
        }
    }

    private static bool IsDeadlock(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 1205 }) return true;
        return false;
    }
}
