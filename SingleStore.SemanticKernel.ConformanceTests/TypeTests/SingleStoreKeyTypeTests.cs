using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreKeyTypeTests(SingleStoreKeyTypeTests.Fixture fixture)
    : KeyTypeTests(fixture), IClassFixture<SingleStoreKeyTypeTests.Fixture>
{
    public new class Fixture : KeyTypeTests.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
