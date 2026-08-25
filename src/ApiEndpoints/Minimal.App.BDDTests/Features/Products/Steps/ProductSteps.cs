using System.Linq;
using System.Net.Http.Json;
using Minimal.AppServices.AutomatedSample.V1;

namespace Minimal.App.BDDTests.Features.Products.Steps;

[Binding]
public sealed class ProductSteps(HttpClient client, ScenarioState state, BddApiFactory factory)
{
    private Guid _lastId;

    #region When

    [When(@"I create a product named ""(.*)"" with price (.*)")]
    public Task WhenICreateAProduct(string name, decimal price) => CreateAsync(name, price);

    [When(@"I get the product with id ""(.*)""")]
    public async Task WhenIGetTheProductWithId(Guid id)
    {
        state.Response = await client.GetAsync($"/v1/products/{id}");
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [When("I get that product")]
    public async Task WhenIGetThatProduct()
    {
        state.Response = await client.GetAsync($"/v1/products/{_lastId}");
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [When("I list products")]
    public async Task WhenIListProducts()
    {
        state.Response = await client.GetAsync("/v1/products");
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [When(@"I change that product's price to (.*)")]
    public async Task WhenIChangeThatProductsPriceTo(decimal price)
    {
        state.Response = await client.PutAsJsonAsync($"/v1/products/{_lastId}", new { price });
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    [When("I delete that product")]
    public async Task WhenIDeleteThatProduct()
    {
        state.Response = await client.DeleteAsync($"/v1/products/{_lastId}");
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    #endregion

    #region Given

    [Given(@"a product exists named ""(.*)"" with price (.*)")]
    public async Task GivenAProductExists(string name, decimal price)
    {
        await CreateAsync(name, price);
        state.Response!.IsSuccessStatusCode.ShouldBeTrue();
    }

    #endregion

    #region Then

    [Then(@"the product response has name ""(.*)"" and price (.*)")]
    public void ThenTheProductResponseHasNameAndPrice(string name, decimal price)
    {
        var dto = Deserialize();
        dto.Name.ShouldBe(name);
        dto.Price.ShouldBe(price);
    }

    [Then(@"the product response has price (.*)")]
    public void ThenTheProductResponseHasPrice(decimal price) => Deserialize().Price.ShouldBe(price);

    [Then(@"the response includes a product named ""(.*)""")]
    public void ThenTheResponseIncludesAProductNamed(string name)
    {
        // Unlike ManualSample's hand-written list (a bare JSON array — see PurchaseOrderSteps), the generic
        // MapGetList<TEntity,TKey,TDto>() library route wraps its page in a { items, pageCount, ... } envelope.
        state.ResponseBody.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(state.ResponseBody!);
        var items = doc.RootElement.GetProperty("items");
        items.EnumerateArray().Any(e => e.GetProperty("name").GetString() == name).ShouldBeTrue();
    }

    [Then("a log line reports the automated sample product was created")]
    public void ThenALogLineReportsTheAutomatedSampleProductWasCreated() =>
        factory.LogCapture.Messages.ShouldContain(m => m.Contains("AutomatedSample product created", StringComparison.Ordinal));

    #endregion

    private async Task CreateAsync(string name, decimal price)
    {
        state.Response = await client.PostAsJsonAsync("/v1/products", new { name, price });
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();

        if (state.Response.IsSuccessStatusCode)
        {
            var dto = JsonSerializer.Deserialize<ProductDto>(state.ResponseBody, SharedConsts.JsonSerializerOptions);
            _lastId = dto!.Id;
        }
    }

    private ProductDto Deserialize()
    {
        state.ResponseBody.ShouldNotBeNullOrEmpty();
        return JsonSerializer.Deserialize<ProductDto>(state.ResponseBody!, SharedConsts.JsonSerializerOptions)!;
    }
}
