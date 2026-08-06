using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreDataTypeTests(SingleStoreDataTypeTests.Fixture fixture)
    : DataTypeTests<Guid, DataTypeTests<Guid>.DefaultRecord>(fixture), IClassFixture<SingleStoreDataTypeTests.Fixture>
{
    public new class Fixture : DataTypeTests<Guid, DefaultRecord>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
