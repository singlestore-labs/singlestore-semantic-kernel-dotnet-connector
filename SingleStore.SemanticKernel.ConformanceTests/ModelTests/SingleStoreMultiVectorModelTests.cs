using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests.ModelTests;

public class SingleStoreMultiVectorModelTests(SingleStoreMultiVectorModelTests.Fixture fixture)
    : MultiVectorModelTests<string>(fixture), IClassFixture<SingleStoreMultiVectorModelTests.Fixture>
{
    public new class Fixture : MultiVectorModelTests<string>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
