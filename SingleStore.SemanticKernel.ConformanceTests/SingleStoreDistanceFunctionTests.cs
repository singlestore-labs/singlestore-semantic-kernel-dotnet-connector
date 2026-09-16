using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreDistanceFunctionTests(SingleStoreDistanceFunctionTests.Fixture fixture)
    : DistanceFunctionTests<int>(fixture), IClassFixture<SingleStoreDistanceFunctionTests.Fixture>
{
    public override Task CosineDistance()
    {
        return Assert.ThrowsAsync<NotSupportedException>(base.CosineDistance);
    }

    public override Task CosineSimilarity()
    {
        return Assert.ThrowsAsync<NotSupportedException>(base.CosineSimilarity);
    }

    public override Task HammingDistance()
    {
        return Assert.ThrowsAsync<NotSupportedException>(base.HammingDistance);
    }

    public override Task ManhattanDistance()
    {
        return Assert.ThrowsAsync<NotSupportedException>(base.ManhattanDistance);
    }

    public new class Fixture : DistanceFunctionTests<int>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
