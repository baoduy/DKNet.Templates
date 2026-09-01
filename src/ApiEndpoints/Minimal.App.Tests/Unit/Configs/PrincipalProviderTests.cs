using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Minimal.Api.Configs.Handlers;
using Minimal.AppServices.Share;
using Minimal.Share;
using Moq;

namespace Minimal.App.Tests.Unit.Configs;

/// <summary>
/// Unit-level coverage of <see cref="PrincipalProvider"/>'s claim resolution — the row-level authorization
/// boundary <c>DataOwnerHook</c>/<c>DataOwnerAuthQuery</c> stamp and filter on. Exercised directly against a
/// fake <see cref="IHttpContextAccessor"/> so every claim shape (present/absent/empty, GUID/non-GUID,
/// precedence order) is reachable without booting a host. Isolation-under-real-query-filtering and the
/// deny-closed read path are covered at the integration level in
/// <c>Integration.AutomatedSample.V1.ProductOwnershipIsolationTests</c> — this class proves the key the
/// filter is fed, not the filter itself.
/// </summary>
public sealed class PrincipalProviderTests
{
    #region Methods

    [Fact]
    public void GetOwnershipKey_ShouldReturnRawGuidSubject_WhenNameIdentifierIsAGuid()
    {
        var provider = CreateProvider(AuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "11111111-2222-3333-4444-555555555555")));

        provider.GetOwnershipKey().ShouldBe("11111111-2222-3333-4444-555555555555");
        provider.ProfileId.ShouldBe(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    }

    [Fact]
    public void GetOwnershipKey_ShouldReturnDistinctKeys_ForTwoCallersWithDifferentNonGuidSubjects()
    {
        // The regression this cycle exists to prevent: a collapse makes every caller resolve to the SAME
        // value, so the assertion must compare two independently-resolved instances against each other, not
        // just each against a hard-coded literal.
        var providerA = CreateProvider(AuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "opaque-subject-a")));
        var providerB = CreateProvider(AuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "opaque-subject-b")));

        var keyA = providerA.GetOwnershipKey();
        var keyB = providerB.GetOwnershipKey();

        keyA.ShouldBe("opaque-subject-a");
        keyB.ShouldBe("opaque-subject-b");
        keyA.ShouldNotBe(keyB);
        keyA.ShouldNotBe(Guid.Empty.ToString());
        keyB.ShouldNotBe(Guid.Empty.ToString());
    }

    [Fact]
    public void GetOwnershipKey_ShouldPreferObjectIdentifier_OverNameIdentifier()
    {
        // The Entra v2.0 shape that motivated the finding: oid is a GUID, NameIdentifier (sub) is opaque.
        var provider = CreateProvider(AuthenticatedContext(
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier",
                "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            new Claim(ClaimTypes.NameIdentifier, "opaque-pairwise-sub")));

        provider.GetOwnershipKey().ShouldBe("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    }

    [Fact]
    public void GetOwnershipKey_ShouldFallBackToOid_WhenObjectIdentifierClaimIsAbsent()
    {
        var provider = CreateProvider(AuthenticatedContext(
            new Claim("oid", "bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            new Claim(ClaimTypes.NameIdentifier, "opaque-pairwise-sub")));

        provider.GetOwnershipKey().ShouldBe("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    }

    [Fact]
    public void GetOwnershipKey_ShouldFallBackToSub_WhenNoOtherSubjectClaimResolves()
    {
        var provider = CreateProvider(AuthenticatedContext(new Claim("sub", "opaque-sub-only")));

        provider.GetOwnershipKey().ShouldBe("opaque-sub-only");
    }

    [Fact]
    public void GetOwnershipKey_ShouldSkipEmptyClaimValue_AndFallThroughToNextInPrecedence()
    {
        var provider = CreateProvider(AuthenticatedContext(
            new Claim("oid", "   "),
            new Claim(ClaimTypes.NameIdentifier, "opaque-pairwise-sub")));

        provider.GetOwnershipKey().ShouldBe("opaque-pairwise-sub");
    }

    [Fact]
    public void GetOwnershipKey_ShouldReturnNull_WhenAuthenticatedCallerHasNoResolvableSubjectClaim()
    {
        var provider = CreateProvider(AuthenticatedContext());

        provider.GetOwnershipKey().ShouldBeNull();
    }

    [Fact]
    public void GetAccessibleKeys_ShouldBeEmpty_WhenAuthenticatedCallerHasNoResolvableSubjectClaim()
    {
        // R3 — deny-closed, never a shared placeholder. Assert emptiness of the collection itself, not just
        // that the key is null: an equality check on the key alone would not catch a default implementation
        // that still wraps a null/blank key into a non-empty collection.
        var provider = CreateProvider(AuthenticatedContext());

        provider.GetAccessibleKeys().ShouldBeEmpty();
    }

    [Fact]
    public void ProfileId_ShouldBeGuidEmpty_WhenAuthenticatedCallerHasNoResolvableSubjectClaim()
    {
        var provider = CreateProvider(AuthenticatedContext());

        provider.ProfileId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void GetOwnershipKey_ShouldBeSystemAccount_WhenCallerIsUnauthenticated()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        var provider = CreateProvider(context);

        provider.GetOwnershipKey().ShouldBe(SharedConsts.SystemAccount);
    }

    [Fact]
    public void GetOwnershipKey_ShouldBeNull_WhenNoHttpContextIsAvailable()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        IPrincipalProvider provider = new PrincipalProvider(accessor.Object);

        provider.GetOwnershipKey().ShouldBeNull();
        provider.GetAccessibleKeys().ShouldBeEmpty();
    }

    [Fact]
    public void GetOwnershipKey_ShouldResolveOnce_AndNotReactToClaimsChangedAfterFirstRead()
    {
        // Memoisation guard: a scoped instance resolves identity once per HttpContext, not on every property
        // read — otherwise an unresolved principal would re-run resolution (and any future logging/telemetry
        // hung off it would fire) on every access.
        var context = AuthenticatedContext(new Claim(ClaimTypes.NameIdentifier, "first-subject"));
        var provider = CreateProvider(context);

        var first = provider.GetOwnershipKey();

        context.User = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, "second-subject"));
        var second = provider.GetOwnershipKey();

        first.ShouldBe("first-subject");
        second.ShouldBe("first-subject");
    }

    [Fact]
    public void Email_ShouldResolveFromEmailClaim_AndUserName_ShouldKeepIdentityName_WhenBothArePresent()
    {
        var provider = CreateProvider(AuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "11111111-2222-3333-4444-555555555555"),
            new Claim(ClaimTypes.Name, "alice"),
            new Claim("email", "alice@example.com")));

        provider.Email.ShouldBe("alice@example.com");
        provider.UserName.ShouldBe("alice");
    }

    [Fact]
    public void UserName_ShouldFallBackToEmail_WhenIdentityNameIsAbsent()
    {
        var provider = CreateProvider(AuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "11111111-2222-3333-4444-555555555555"),
            new Claim("emails", "bob@example.com")));

        provider.Email.ShouldBe("bob@example.com");
        provider.UserName.ShouldBe("bob@example.com");
    }

    private static IPrincipalProvider CreateProvider(HttpContext context)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);
        return new PrincipalProvider(accessor.Object);
    }

    private static DefaultHttpContext AuthenticatedContext(params Claim[] claims) =>
        new() { User = BuildPrincipal(claims) };

    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    #endregion
}
