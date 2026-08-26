using Microsoft.Extensions.VectorData;
using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreDataTypeTests(SingleStoreDataTypeTests.Fixture fixture)
    : DataTypeTests<Guid, DataTypeTests<Guid>.DefaultRecord>(fixture), IClassFixture<SingleStoreDataTypeTests.Fixture>
{
    protected override object? GenerateEmptyProperty(VectorStoreProperty property)
        => property.Type switch
        {
            var t when t == typeof(DateTime) => new DateTime(1000, 1, 1),
            var t when t == typeof(DateTimeOffset) => new DateTimeOffset(1000, 1, 1, 0, 0, 0, TimeSpan.Zero),
#if NET
            var t when t == typeof(DateOnly) => new DateOnly(1000, 1, 1),
#endif
            _ => base.GenerateEmptyProperty(property)
        };

    // SingleStore does not support representing an offset, so only DateTimeOffsets with offset=0 are supported.
    public override Task DateTimeOffset()
    {
        return Assert.ThrowsAsync<ArgumentException>(base.DateTimeOffset);
    }

    // SingleStore does not support representing an offset, so only DateTimeOffsets with offset=0 are supported.
    public virtual Task DateTimeOffset_with_offset_zero()
    {
        return Test(
            "DateTimeOffset",
            new DateTimeOffset(2020, 1, 1, 12, 30, 45, TimeSpan.FromHours(0)),
            new DateTimeOffset(2021, 2, 3, 13, 40, 55, TimeSpan.FromHours(0)),
            instantiationExpression: () => new DateTimeOffset(2020, 1, 1, 12, 30, 45, TimeSpan.FromHours(0)));
    }

    public new class Fixture : DataTypeTests<Guid, DefaultRecord>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
