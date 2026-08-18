using System.Text.Json.Nodes;

namespace Minimal.App.BDDTests.Features.LoyaltyMemberships.Steps;

/// <summary>
/// Steps for the fixed "Alice Nguyen" scenarios in <c>LoyaltyMembershipEvents.feature</c> — the member name
/// never varies across scenarios, so step text is literal rather than parameterized (avoids the "Nguyen's"
/// possessive tripping up a {word} capture).
/// </summary>
[Binding]
public sealed class LoyaltyMembershipEventsSteps(HttpClient client, ScenarioState state, BddApiFactory factory)
{
    private const string Url = "/v1/loyalty-memberships";
    private const string MemberName = "Alice Nguyen";

    private Guid _membershipId;

    [When("Alice Nguyen is enrolled in the loyalty programme at tier {word} with {int} points")]
    public async Task WhenAliceNguyenIsEnrolled(string tier, int points)
    {
        await SendEnroll(tier, points);
    }

    [Given("Alice Nguyen holds a {word} loyalty membership")]
    public async Task GivenAliceNguyenHoldsALoyaltyMembership(string tier)
    {
        await SendEnroll(tier, 0);
        _membershipId = await ExtractMembershipId(state.Response!);
    }

    [Given("Alice Nguyen holds a {word} loyalty membership with {int} points")]
    public async Task GivenAliceNguyenHoldsALoyaltyMembershipWithPoints(string tier, int points)
    {
        await SendEnroll(tier, points);
        _membershipId = await ExtractMembershipId(state.Response!);
    }

    [Given("Alice Nguyen already holds a loyalty membership")]
    public async Task GivenAliceNguyenAlreadyHoldsALoyaltyMembership()
    {
        await SendEnroll("Bronze", 0);
        state.Response!.IsSuccessStatusCode.ShouldBeTrue();

        // Wait for the seeding enrolment's own log line(s) to land, then clear — otherwise the "no line
        // reporting an enrolment" check below could see leftover output from this seed, not from the rejection.
        await Eventually.IsTrueAsync(() => factory.LogCapture.Messages.Any(m =>
            m.Contains("enrolled", StringComparison.OrdinalIgnoreCase)));
        factory.LogCapture.Clear();
    }

    [When("her membership is changed to tier {word}")]
    public async Task WhenHerMembershipIsChangedToTier(string tier)
    {
        await SendChange(_membershipId, tier, points: null);
    }

    [When("her points balance is changed to {int} and her tier is left at {word}")]
    public async Task WhenHerPointsBalanceIsChanged(int points, string tier)
    {
        await SendChange(_membershipId, tier: null, points: points);
    }

    [When("her loyalty membership is withdrawn")]
    public async Task WhenHerLoyaltyMembershipIsWithdrawn()
    {
        state.Response = await client.DeleteAsync($"{Url}?Id={_membershipId}");
    }

    [When("a second loyalty membership is requested for Alice Nguyen")]
    public async Task WhenASecondLoyaltyMembershipIsRequested()
    {
        await SendEnroll("Silver", 0);
    }

    [Then("the enrolment is stored")]
    public void ThenTheEnrolmentIsStored()
    {
        state.Response.ShouldNotBeNull();
        state.Response!.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Then("the application log contains one line reporting Alice Nguyen's enrolment")]
    public async Task ThenTheApplicationLogContainsOneLineReportingHerEnrolment()
    {
        Func<bool> matches = () => factory.LogCapture.Messages.Any(m =>
            m.Contains("enrolled", StringComparison.OrdinalIgnoreCase) &&
            m.Contains(MemberName, StringComparison.Ordinal));

        (await Eventually.IsTrueAsync(matches)).ShouldBeTrue();
        factory.LogCapture.Messages.Count(m =>
                m.Contains("enrolled", StringComparison.OrdinalIgnoreCase) &&
                m.Contains(MemberName, StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Then("the application log contains one line reporting her tier change to {word}")]
    public async Task ThenTheApplicationLogContainsOneLineReportingHerTierChangeTo(string tier)
    {
        Func<bool> matches = () => factory.LogCapture.Messages.Any(m =>
            m.Contains("tier changed to", StringComparison.OrdinalIgnoreCase) &&
            m.Contains(tier, StringComparison.Ordinal));

        (await Eventually.IsTrueAsync(matches)).ShouldBeTrue();
        factory.LogCapture.Messages.Count(m =>
                m.Contains("tier changed to", StringComparison.OrdinalIgnoreCase) &&
                m.Contains(tier, StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Then("the application log contains no line reporting a tier change")]
    public async Task ThenTheApplicationLogContainsNoLineReportingATierChange()
    {
        // Negative assertion: the [RaisesEvent] narrowing on nameof(Tier) means the tier-changed event was
        // never queued for a points-only save — there's nothing to wait for. A short settle delay still
        // guards against a regression that queues it anyway but publishes it slightly late.
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        factory.LogCapture.Messages.ShouldNotContain(m =>
            m.Contains("tier changed", StringComparison.OrdinalIgnoreCase));
    }

    [Then("the application log contains one line reporting the withdrawal at tier {word} with {int} points")]
    public async Task ThenTheApplicationLogContainsOneLineReportingTheWithdrawal(string tier, int points)
    {
        Func<bool> matches = () => factory.LogCapture.Messages.Any(m =>
            m.Contains("withdrawn", StringComparison.OrdinalIgnoreCase) &&
            m.Contains(tier, StringComparison.Ordinal) &&
            m.Contains(points.ToString(), StringComparison.Ordinal));

        (await Eventually.IsTrueAsync(matches)).ShouldBeTrue();
        factory.LogCapture.Messages.Count(m =>
                m.Contains("withdrawn", StringComparison.OrdinalIgnoreCase) &&
                m.Contains(tier, StringComparison.Ordinal) &&
                m.Contains(points.ToString(), StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Then("the application log contains no line reporting an enrolment")]
    public void ThenTheApplicationLogContainsNoLineReportingAnEnrolment()
    {
        factory.LogCapture.Messages.ShouldNotContain(m =>
            m.Contains("enrolled", StringComparison.OrdinalIgnoreCase));
    }

    private async Task SendEnroll(string tier, int points)
    {
        var body = new JsonObject
        {
            ["memberName"] = MemberName,
            ["tier"] = tier.ToLowerInvariant(),
            ["points"] = points
        };
        var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

        state.Response = await client.SendAsync(request);
    }

    private async Task SendChange(Guid id, string? tier, int? points)
    {
        var body = new JsonObject { ["id"] = id.ToString() };
        if (tier is not null)
        {
            body["tier"] = tier.ToLowerInvariant();
        }

        if (points is not null)
        {
            body["points"] = points.Value;
        }

        state.Response = await client.PutAsync(
            Url,
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
    }

    private static async Task<Guid> ExtractMembershipId(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }
}
