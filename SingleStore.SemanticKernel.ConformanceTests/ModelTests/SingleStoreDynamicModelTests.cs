using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests.ModelTests;

public class SingleStoreDynamicModelTests(SingleStoreDynamicModelTests.Fixture fixture)
    : DynamicModelTests<string>(fixture), IClassFixture<SingleStoreDynamicModelTests.Fixture>
{
    public new class Fixture : DynamicModelTests<string>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
