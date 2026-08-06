using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreHybridSearchTests(
    SingleStoreHybridSearchTests.VectorAndStringFixture vectorAndStringFixture,
    SingleStoreHybridSearchTests.MultiTextFixture multiTextFixture)
    : HybridSearchTests<long>(vectorAndStringFixture, multiTextFixture),
        IClassFixture<SingleStoreHybridSearchTests.VectorAndStringFixture>,
        IClassFixture<SingleStoreHybridSearchTests.MultiTextFixture>
{
    public new class VectorAndStringFixture : HybridSearchTests<long>.VectorAndStringFixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }

    public new class MultiTextFixture : HybridSearchTests<long>.MultiTextFixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
