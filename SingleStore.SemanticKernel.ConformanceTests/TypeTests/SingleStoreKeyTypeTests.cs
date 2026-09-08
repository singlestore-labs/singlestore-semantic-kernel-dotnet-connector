using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreKeyTypeTests(SingleStoreKeyTypeTests.Fixture fixture)
    : KeyTypeTests(fixture), IClassFixture<SingleStoreKeyTypeTests.Fixture>
{
    [Fact]
    public virtual Task Short()
    {
        return Test<short>(8);
    }

    [Fact]
    public virtual Task Int()
    {
        return Test(8);
    }

    [Fact]
    public virtual Task Long()
    {
        return Test(8L);
    }

    [Fact]
    public virtual Task String()
    {
        return Test<string>("foo", "bar");
    }

    public new class Fixture : KeyTypeTests.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
