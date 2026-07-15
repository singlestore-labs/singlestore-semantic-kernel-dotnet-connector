using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreFilterTests(SingleStoreFilterTests.Fixture fixture)
    : FilterTests<int>(fixture), IClassFixture<SingleStoreFilterTests.Fixture>
{
    public new class Fixture : FilterTests<int>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
