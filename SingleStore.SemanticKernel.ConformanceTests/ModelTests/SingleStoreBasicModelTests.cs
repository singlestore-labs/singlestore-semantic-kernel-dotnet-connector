using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests.ModelTests;

public class SingleStoreBasicModelTests(SingleStoreBasicModelTests.Fixture fixture)
    : BasicModelTests<string>(fixture), IClassFixture<SingleStoreBasicModelTests.Fixture>
{
    public new class Fixture : BasicModelTests<string>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
