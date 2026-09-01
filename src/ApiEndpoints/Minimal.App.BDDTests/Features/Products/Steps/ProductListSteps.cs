using System.Globalization;
using System.Linq;
using System.Net.Http.Json;

namespace Minimal.App.BDDTests.Features.Products.Steps;

/// <summary>
/// Steps for the generic list-query contract (filter/search/order/page) exposed by
/// <c>MapGetList&lt;Product, Guid, ProductDto&gt;()</c>. Kept apart from <see cref="ProductSteps"/>: those
/// prove the CRUD lifecycle, these fence the read-query surface. See docs/generic-list-endpoint.md.
/// </summary>
[Binding]
public sealed class ProductListSteps(HttpClient client, ScenarioState state)
{
    #region Given

    [Given("the following products exist:")]
    public async Task GivenTheFollowingProductsExist(Table table)
    {
        foreach (var row in table.Rows)
        {
            var name = row["name"];
            var price = decimal.Parse(row["price"], CultureInfo.InvariantCulture);
            var response = await client.PostAsJsonAsync("/v1/products", new { name, price });
            response.IsSuccessStatusCode.ShouldBeTrue($"seeding product '{name}' should succeed");
        }
    }

    #endregion

    #region When

    [When(@"I list products with query ""(.*)""")]
    public async Task WhenIListProductsWithQuery(string query)
    {
        state.Response = await client.GetAsync($"/v1/products{query}");
        state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    }

    #endregion

    #region Then

    [Then(@"the list contains exactly (\d+) products")]
    public void ThenTheListContainsExactlyNProducts(int count) => ItemNames().Count.ShouldBe(count);

    [Then(@"the list contains exactly ""(.*)""")]
    public void ThenTheListContainsExactly(string names)
    {
        var expected = Split(names);
        // Order-independent set equality — pins both inclusion AND exclusion AND the total count, so a
        // filter that returns too many or too few rows fails here.
        ItemNames().OrderBy(n => n).ShouldBe(expected.OrderBy(n => n));
    }

    [Then(@"the listed products are in order ""(.*)""")]
    public void ThenTheListedProductsAreInOrder(string names) => ItemNames().ShouldBe(Split(names));

    [Then(@"the paged response ""(.*)"" is ""(.*)""")]
    public void ThenThePagedResponseFieldIs(string field, string expected)
    {
        using var doc = JsonDocument.Parse(state.ResponseBody!);
        // GetRawText() yields the literal JSON token — "4", "100", "true", "false" — matching the values
        // the feature file asserts for numeric and boolean envelope fields.
        doc.RootElement.GetProperty(field).GetRawText().ShouldBe(expected);
    }

    #endregion

    private List<string> ItemNames()
    {
        state.ResponseBody.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(state.ResponseBody!);
        return doc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToList();
    }

    private static string[] Split(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
