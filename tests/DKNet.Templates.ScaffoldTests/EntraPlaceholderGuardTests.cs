
namespace DKNet.Templates.ScaffoldTests;

/// <summary>DRK-1233 R2-R5: synthetic-JSON scenarios for <see cref="EntraPlaceholderGuard"/>.</summary>
public class EntraPlaceholderGuardTests
{
    private const string Tenant = "00000000-0000-0000-0000-000000000000";
    private const string Audience = "api://your-api";
    private const string OtherGuid = "11111111-1111-1111-1111-111111111111";

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static JsonElement LocalBearer() => Parse("""
        {
          "ValidAudiences": ["http://localhost:5000"],
          "ValidIssuer": "dotnet-user-jwts"
        }
        """);

    private static JsonElement MatchingEntraBearer() => Parse($$"""
        {
          "MetadataAddress": "https://login.microsoftonline.com/{{Tenant}}/v2.0/.well-known/openid-configuration",
          "ValidIssuer": "https://sts.windows.net/{{Tenant}}/",
          "ValidAudiences": ["{{Audience}}"]
        }
        """);

    [Fact]
    public void BearerSectionWithNoMetadataAddress_IsNotEntraShaped()
    {
        EntraPlaceholderGuard.IsEntraShaped(LocalBearer()).ShouldBeFalse();
    }

    [Fact]
    public void LocalOnlyBearerSection_IsSkippedAndReportedAsUnchecked()
    {
        var result = EntraPlaceholderGuard.Check(
            [("appsettings.Development.json", LocalBearer())], Tenant, Audience);

        result.EntraSectionsChecked.ShouldBe(0);
        result.Offenders.ShouldBeEmpty();
    }

    [Fact]
    public void MatchingEntraSection_PassesAndCountsAsChecked()
    {
        var result = EntraPlaceholderGuard.Check(
            [("appsettings.json", MatchingEntraBearer())], Tenant, Audience);

        result.EntraSectionsChecked.ShouldBe(1);
        result.Offenders.ShouldBeEmpty();
    }

    [Fact]
    public void LocalSectionAlongsideMatchingEntraSection_StillCountsExactlyOne()
    {
        var result = EntraPlaceholderGuard.Check(
            [("appsettings.Development.json", LocalBearer()), ("appsettings.json", MatchingEntraBearer())],
            Tenant, Audience);

        result.EntraSectionsChecked.ShouldBe(1);
        result.Offenders.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("MetadataAddress")]
    [InlineData("ValidIssuer")]
    [InlineData("ValidAudiences")]
    public void DriftInAShippedEntraConfig_IsStillReported(string field)
    {
        var bearer = field switch
        {
            "MetadataAddress" => Parse($$"""
                {
                  "MetadataAddress": "https://login.microsoftonline.com/{{OtherGuid}}/v2.0/.well-known/openid-configuration",
                  "ValidIssuer": "https://sts.windows.net/{{Tenant}}/",
                  "ValidAudiences": ["{{Audience}}"]
                }
                """),
            "ValidIssuer" => Parse($$"""
                {
                  "MetadataAddress": "https://login.microsoftonline.com/{{Tenant}}/v2.0/.well-known/openid-configuration",
                  "ValidIssuer": "https://sts.windows.net/{{OtherGuid}}/",
                  "ValidAudiences": ["{{Audience}}"]
                }
                """),
            "ValidAudiences" => Parse($$"""
                {
                  "MetadataAddress": "https://login.microsoftonline.com/{{Tenant}}/v2.0/.well-known/openid-configuration",
                  "ValidIssuer": "https://sts.windows.net/{{Tenant}}/",
                  "ValidAudiences": ["api://someone-elses-api"]
                }
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var result = EntraPlaceholderGuard.Check([("appsettings.json", bearer)], Tenant, Audience);

        result.EntraSectionsChecked.ShouldBe(1);
        result.Offenders.Count.ShouldBe(1);
        result.Offenders[0].ShouldContain(field);
    }

    [Theory]
    [InlineData("ValidIssuer")]
    [InlineData("ValidAudiences")]
    public void AnEntraSectionMissingARequiredField_IsAnOffenderNotACrash(string missing)
    {
        var bearer = missing switch
        {
            "ValidIssuer" => Parse($$"""
                {
                  "MetadataAddress": "https://login.microsoftonline.com/{{Tenant}}/v2.0/.well-known/openid-configuration",
                  "ValidAudiences": ["{{Audience}}"]
                }
                """),
            "ValidAudiences" => Parse($$"""
                {
                  "MetadataAddress": "https://login.microsoftonline.com/{{Tenant}}/v2.0/.well-known/openid-configuration",
                  "ValidIssuer": "https://sts.windows.net/{{Tenant}}/"
                }
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(missing)),
        };

        var exception = Record.Exception(() =>
            EntraPlaceholderGuard.Check([("appsettings.json", bearer)], Tenant, Audience));
        exception.ShouldBeNull();

        var result = EntraPlaceholderGuard.Check([("appsettings.json", bearer)], Tenant, Audience);
        result.EntraSectionsChecked.ShouldBe(1);
        result.Offenders.Count.ShouldBe(1);
        result.Offenders[0].ShouldContain(missing);
    }
}
