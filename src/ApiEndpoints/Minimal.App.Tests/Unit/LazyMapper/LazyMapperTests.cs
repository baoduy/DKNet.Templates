using Minimal.App.Tests.Data;
using Minimal.AppServices.Extensions.LazyMapper;

namespace Minimal.App.Tests.Unit.LazyMapper;

public class LazyMapperTests(LazyMapFixture fixture) : IClassFixture<LazyMapFixture>
{
    #region Methods

    [Fact]
    public void LazyMapNullValueTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var v = mapper.LazyMap<TestDataModel>(null!);
        v.ValueOrDefault.ShouldBeNull();
    }

    [Fact]
    public void LazyMapNullValueThrowExceptionTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var v = mapper.LazyMap<TestDataModel>(null!);
        Func<object> a = () => v.Value;
        a.ShouldThrow<Exception>();
    }

    [Fact]
    public void LazyMapTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var m = new TestDataModel("Steven");
        var v = mapper.LazyMap<View>(m);

        v.Value.ShouldNotBeNull();
        v.Value.Id.ShouldBe(m.Id);
        v.Value.Name.ShouldBe(m.Name);
    }

    [Fact]
    public void LazyMapTheSameTypeTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var m = new TestDataModel("Steven");
        var v = mapper.LazyMap<TestDataModel>(m);

        v.Value.ShouldNotBeNull();
        v.Value.ShouldBe(m);
    }

    #endregion
}