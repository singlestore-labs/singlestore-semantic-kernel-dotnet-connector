using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreEmbeddingTypeTests(SingleStoreEmbeddingTypeTests.Fixture fixture)
    : EmbeddingTypeTests<int>(fixture), IClassFixture<SingleStoreEmbeddingTypeTests.Fixture>
{
    public new class Fixture : EmbeddingTypeTests<int>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
