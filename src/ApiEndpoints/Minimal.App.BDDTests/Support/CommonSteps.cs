using System.Linq;

namespace Minimal.App.BDDTests.Support;

/// <summary>
/// Steps shared by more than one DRK-455 feature file (the PurchaseOrder and Product scenarios use
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

    [Then(@"the response status is (\d+)")]
    public void ThenTheResponseStatusIs(int statusCode)
    {
        state.Response.ShouldNotBeNull();
        ((int)state.Response!.StatusCode).ShouldBe(statusCode);
    }

    // DRK-1410: the spec's own Gherkin wording ("the response is <code>") differs from this repo's
    // pre-existing wording above ("the response status is <code>") — kept as a separate step rather than
    // reworded, since the acceptance criteria are frozen text, not paraphrased to match house style.
    [Then(@"the response is (\d+)")]
    public void ThenTheResponseIs(int statusCode)
    {
        state.Response.ShouldNotBeNull();
        ((int)state.Response!.StatusCode).ShouldBe(statusCode);
    }

    // DRK-1410: the BDD host runs with FeatureManagement:RequireAuthorization=false (BddApiFactory), so
    // no policy is ever evaluated here — "holds the scope" is satisfied by the route staying reachable at
    // all. Scope-gated variants belong in Minimal.App.Tests/Integration against AuthOnApiFixture instead.
    [Given(@"catalogue-ops holds the scope ""(.*)""")]
    public void GivenCatalogueOpsHoldsTheScope(string scope)
    {
    }

    [Given("catalogue-ops holds every scope the operation needs")]
    public void GivenCatalogueOpsHoldsEveryScopeTheOperationNeeds()
    {
    }

    [Given("catalogue-ops is signed in")]
    public void GivenCatalogueOpsIsSignedIn()
    {
    }

    [Then("the response body carries a trace identifier")]
    public void ThenTheResponseBodyCarriesATraceIdentifier()
    {
        state.ResponseBody.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(state.ResponseBody!);
        doc.RootElement.TryGetProperty("trace-id", out var traceId).ShouldBeTrue();
        traceId.GetString().ShouldNotBeNullOrEmpty();
    }

    [Then("the response body carries a code naming the rule that refused")]
    public void ThenTheResponseBodyCarriesACodeNamingTheRuleThatRefused()
    {
        state.ResponseBody.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(state.ResponseBody!);
        doc.RootElement.TryGetProperty("code", out var code).ShouldBeTrue();
        code.GetString().ShouldNotBeNullOrEmpty();
    }

    [Then("the response names the field it refused")]
    public void ThenTheResponseNamesTheFieldItRefused()
    {
        state.ResponseBody.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(state.ResponseBody!);
        doc.RootElement.TryGetProperty("errors", out var errors).ShouldBeTrue();
        errors.EnumerateObject().Any().ShouldBeTrue();
    }
}
