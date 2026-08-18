namespace Minimal.App.BDDTests.Features.CustomerProfiles.Steps;

[Binding]
public sealed class IdempotencySteps(HttpClient client, ScenarioState state, BddApiFactory factory)
{
    private const string CreateUrl = "/v1/customer-profiles";
    private const string BaoDuyEmail = "bao.duy.idempotency@example.com";

    private Guid _firstProfileId;

    [Given("a customer profile has been created for Bao Duy with idempotency key {string}")]
    public async Task GivenACustomerProfileHasBeenCreatedForBaoDuyWithIdempotencyKey(string idempotencyKey)
    {
        state.Response = await SendCreateRequest(idempotencyKey);
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
        state.Response.IsSuccessStatusCode.ShouldBeTrue();

        using var doc = JsonDocument.Parse(state.ResponseBody);
        _firstProfileId = doc.RootElement.GetProperty("id").GetGuid();
    }

    [When("the same create request is sent again with idempotency key {string}")]
    public async Task WhenTheSameCreateRequestIsSentAgainWithIdempotencyKey(string idempotencyKey)
    {
        state.Response = await SendCreateRequest(idempotencyKey);
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [When("a customer profile is requested for Bao Duy without an idempotency key")]
    public async Task WhenACustomerProfileIsRequestedForBaoDuyWithoutAnIdempotencyKey()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CreateUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(), SharedConsts.JsonSerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        state.Response = await client.SendAsync(request);
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [Then("the first request's result is returned")]
    public void ThenTheFirstRequestsResultIsReturned()
    {
        state.Response.ShouldNotBeNull();
        state.Response!.IsSuccessStatusCode.ShouldBeTrue();

        using var doc = JsonDocument.Parse(state.ResponseBody!);
        doc.RootElement.GetProperty("id").GetGuid().ShouldBe(_firstProfileId);
    }

    [Then("only one customer profile exists for Bao Duy")]
    public async Task ThenOnlyOneCustomerProfileExistsForBaoDuy()
    {
        using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var count = await dbContext.Set<CustomerProfile>().CountAsync(p => p.Email == BaoDuyEmail);
        count.ShouldBe(1);
    }

    [Then("no customer profile exists for Bao Duy")]
    public async Task ThenNoCustomerProfileExistsForBaoDuy()
    {
        using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var exists = await dbContext.Set<CustomerProfile>().AnyAsync(p => p.Email == BaoDuyEmail);
        exists.ShouldBeFalse();
    }

    private Task<HttpResponseMessage> SendCreateRequest(string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CreateUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(), SharedConsts.JsonSerializerOptions),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        return client.SendAsync(request);
    }

    private static object CreatePayload() => new
    {
        name = "Bao Duy",
        email = BaoDuyEmail,
        phone = "+6598887766"
    };
}
