using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreCollectionManagementTests(SingleStoreCollectionManagementTests.Fixture fixture)
    : CollectionManagementTests<string>(fixture), IClassFixture<SingleStoreCollectionManagementTests.Fixture>
{
    public class Fixture : VectorStoreFixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
