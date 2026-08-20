using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop.Diagnostics;

internal static class TeardownObserver
{
    public static async Task AwaitAsync(
        Task? operation,
        CancellationToken ownedCancellation,
        string eventCode,
        Action<string, Exception>? report = null)
    {
        if (operation is null)
            return;

        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            // Cancellation requested by this component is normal teardown.
        }
        catch (Exception exception)
        {
            if (report is not null)
                report(eventCode, exception);
            else
                Log.D("Teardown", $"{eventCode}: {exception}");
        }
    }
}
