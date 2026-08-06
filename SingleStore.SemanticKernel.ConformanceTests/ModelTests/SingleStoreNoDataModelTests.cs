using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests.ModelTests;

public class SingleStoreNoDataModelTests(SingleStoreNoDataModelTests.Fixture fixture)
    : NoDataModelTests<string>(fixture), IClassFixture<SingleStoreNoDataModelTests.Fixture>
{
    public new class Fixture : NoDataModelTests<string>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
