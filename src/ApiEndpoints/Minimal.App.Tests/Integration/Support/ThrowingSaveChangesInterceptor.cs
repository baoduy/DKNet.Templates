using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// Simulates the purchase-order store "failing on every write" (the security-headers-on-a-500 scenario) by
/// throwing from the EF Core save pipeline itself, so the failure surfaces as a genuine unhandled exception
/// through <c>GlobalExceptionHandler</c> rather than a crafted test-only endpoint.
/// </summary>
public sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new InvalidOperationException("simulated store failure: every write fails");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("simulated store failure: every write fails");
}
