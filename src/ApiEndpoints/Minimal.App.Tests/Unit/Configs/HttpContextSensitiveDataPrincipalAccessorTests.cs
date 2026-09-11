using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Minimal.Api.Configs.Handlers;
using Moq;

namespace Minimal.App.Tests.Unit.Configs;

/// <summary>
/// Unit-level coverage of <see cref="HttpContextSensitiveDataPrincipalAccessor"/> — the seam
/// <c>UseRoleAwareSensitiveData</c> reads on every serialization (see integration coverage in
/// <c>Integration.AutomatedSample.V1.ProductSensitiveDataTests</c> and
/// <c>ProductSensitiveDataAnonymousTests</c>, both of which only ever run inside an active request and so
/// never reach the no-request branch this class proves directly).
/// </summary>
public sealed class HttpContextSensitiveDataPrincipalAccessorTests
{
    [Fact]
    public void Current_ShouldReturnTheHttpContextsUser_WhenARequestIsActive()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "caller")], "Test"));
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = principal });

        var sut = new HttpContextSensitiveDataPrincipalAccessor(accessor.Object);

        sut.Current.ShouldBeSameAs(principal);
    }

    [Fact]
    public void Current_ShouldReturnNull_WhenNoHttpContextIsAvailable()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var sut = new HttpContextSensitiveDataPrincipalAccessor(accessor.Object);

        sut.Current.ShouldBeNull();
    }
}
