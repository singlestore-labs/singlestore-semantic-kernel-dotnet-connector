using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreIndexKindTests(SingleStoreIndexKindTests.Fixture fixture)
    : IndexKindTests<int>(fixture), IClassFixture<SingleStoreIndexKindTests.Fixture>
{
    public new class Fixture : IndexKindTests<int>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
