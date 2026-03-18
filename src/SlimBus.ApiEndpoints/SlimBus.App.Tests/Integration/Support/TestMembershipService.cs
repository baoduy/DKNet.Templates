using SlimBus.Domains.Services;

namespace SlimBus.App.Tests.Integration.Support;

internal sealed class TestMembershipService : IMembershipService
{
    #region Fields

    private int _current;

    #endregion

    #region Methods

    public ValueTask<string> NextValueAsync()
    {
        var next = Interlocked.Increment(ref _current);
        return ValueTask.FromResult($"TEST-MEM-{next:D6}");
    }

    #endregion
}

