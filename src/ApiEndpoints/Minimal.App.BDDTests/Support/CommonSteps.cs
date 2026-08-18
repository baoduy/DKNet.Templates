namespace Minimal.App.BDDTests.Support;

/// <summary>
/// Steps shared by more than one DRK-455 feature file (idempotency and loyalty-membership scenarios use
/// the same precondition and rejection wording). Kept in one binding so the exact step text is not
/// duplicated — and made ambiguous — across per-feature step classes.
/// </summary>
[Binding]
public sealed class CommonSteps(ScenarioState state)
{
    [Given("the service is running with no Redis connection configured")]
    public void GivenTheServiceIsRunningWithNoRedisConnectionConfigured()
    {
        // The BDD host never sets ConnectionStrings:Redis — this is the default, already-in-effect state.
    }

    [Then("the request is rejected")]
    public void ThenTheRequestIsRejected()
    {
        state.Response.ShouldNotBeNull();
        state.Response!.IsSuccessStatusCode.ShouldBeFalse();
    }
}
