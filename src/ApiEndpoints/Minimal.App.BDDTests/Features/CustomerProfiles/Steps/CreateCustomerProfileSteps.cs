namespace Minimal.App.BDDTests.Features.CustomerProfiles.Steps;

[Binding]
public sealed class CreateCustomerProfileSteps(HttpClient client, ScenarioState state, BddApiFactory factory)
{
    private const string CreateUrl = "/v1/customer-profiles";

    [Given("the API has no customer profiles")]
    public async Task GivenTheApiHasNoCustomerProfiles()
    {
        await factory.ResetDatabaseAsync();
    }

    [Given("a customer profile with email {string} already exists")]
    public async Task GivenACustomerProfileWithEmailAlreadyExists(string email)
    {
        using var seedResponse = await SendCreateRequest(new
        {
            name = "Seed User",
            email,
            phone = "+6500000001"
        });

        seedResponse.IsSuccessStatusCode.ShouldBeTrue();
    }

    [When("I send a create profile request with the following data:")]
    public async Task WhenISendACreateProfileRequestWithTheFollowingData(DataTable table)
    {
        var row = table.Rows[0];
        var payload = new
        {
            name = row["Name"],
            email = row["Email"],
            phone = row["Phone"]
        };

        state.Response = await SendCreateRequest(payload);
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Non-forgeability: <c>ByUser</c> carries no <c>[JsonIgnore]</c>, so a caller can include its own
    /// <c>byUser</c> value in the request body. Proves that value is overwritten by the population filter
    /// before persistence, rather than trusted.
    /// </summary>
    [When("I send a create profile request with byUser {string} and the following data:")]
    public async Task WhenISendACreateProfileRequestWithByUserAndTheFollowingData(string byUser, DataTable table)
    {
        var row = table.Rows[0];
        var payload = new
        {
            name = row["Name"],
            email = row["Email"],
            phone = row["Phone"],
            byUser
        };

        state.Response = await SendCreateRequest(payload);
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [Then("the response should be successful")]
    public void ThenTheResponseShouldBeSuccessful()
    {
        state.Response.ShouldNotBeNull();
        state.ResponseBody.ShouldNotBeNullOrWhiteSpace();
    }

    [Then("the response body should contain the profile name {string}")]
    public async Task ThenTheResponseBodyShouldContainTheProfileName(string expectedName)
    {
        state.ResponseBody.ShouldNotBeNullOrWhiteSpace();
        if (state.ResponseBody!.Contains(expectedName, StringComparison.OrdinalIgnoreCase))
            return;

        using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var exists = await dbContext.Set<CustomerProfile>()
            .AnyAsync(x => x.Name == expectedName);

        exists.ShouldBeTrue();
    }

    [Then("the response body should contain an error message for duplicate email {string}")]
    public void ThenTheResponseBodyShouldContainAnErrorMessageForDuplicateEmail(string email)
    {
        state.Response.ShouldNotBeNull();
        state.Response!.IsSuccessStatusCode.ShouldBeFalse();

        var body = state.ResponseBody;
        body.ShouldNotBeNullOrWhiteSpace();
        body.ShouldContain(email);
    }

    [Then("the response should indicate a validation error")]
    public void ThenTheResponseShouldIndicateAValidationError()
    {
        state.Response.ShouldNotBeNull();
        state.Response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Then("the persisted profile for email {string} should be attributed to the system account, not {string}")]
    public async Task ThenThePersistedProfileForEmailShouldBeAttributedToTheSystemAccountNot(
        string email, string forgedByUser)
    {
        using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var profile = await dbContext.Set<CustomerProfile>().FirstOrDefaultAsync(x => x.Email == email);

        profile.ShouldNotBeNull();
        profile.LastModifiedBy.ShouldNotBe(forgedByUser);
        // BddApiFactory always runs with RequireAuthorization=false (see BddApiFactory.AddFeatureOverrides),
        // so the population filter's fallback is the template's stand-in system account.
        profile.LastModifiedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    private Task<HttpResponseMessage> SendCreateRequest(object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CreateUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SharedConsts.JsonSerializerOptions),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

        return client.SendAsync(request);
    }
}
