using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

#pragma warning disable CA2000 // Dispose objects before losing scope

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreEmbeddingTypeTests(SingleStoreEmbeddingTypeTests.Fixture fixture)
    : EmbeddingTypeTests<int>(fixture), IClassFixture<SingleStoreEmbeddingTypeTests.Fixture>
{
    public new class Fixture : EmbeddingTypeTests<int>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
