using Microsoft.Extensions.VectorData;
using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;

namespace SingleStore.SemanticKernel.ConformanceTests.TypeTests;

public class SingleStoreDataTypeTests(SingleStoreDataTypeTests.Fixture fixture)
    : DataTypeTests<Guid, SingleStoreDataTypeTests.Record>(fixture), IClassFixture<SingleStoreDataTypeTests.Fixture>
{
    // SingleStore does not support representing an offset, so only DateTimeOffsets with offset=0 are supported.
    public override Task DateTimeOffset()
    {
        return Assert.ThrowsAsync<ArgumentException>(base.DateTimeOffset);
    }

    [Fact]

    // SingleStore does not support representing an offset, so only DateTimeOffsets with offset=0 are supported.
    public virtual Task DateTimeOffset_with_offset_zero()
    {
        return Test(
            "DateTimeOffset",
            new DateTimeOffset(2020, 1, 1, 12, 30, 45, TimeSpan.FromHours(0)),
            new DateTimeOffset(2021, 2, 3, 13, 40, 55, TimeSpan.FromHours(0)),
            instantiationExpression: () => new DateTimeOffset(2020, 1, 1, 12, 30, 45, TimeSpan.FromHours(0)));
    }

    // JSON columns do not support equality comparison in filters.
    public override Task String_array()
    {
        return Test<string[]>(
            "StringArray",
            ["foo", "bar"],
            ["foo", "baz"],
            false);
    }

    [Fact]
    public virtual Task SByte()
    {
        return Test<sbyte>("SByte", 8, 9);
    }

    [Fact]
    public virtual Task UShort()
    {
        return Test<ushort>("UShort", 8, 9);
    }

    [Fact]
    public virtual Task UInt()
    {
        return Test<uint>("UInt", 8, 9);
    }

    [Fact]
    public virtual Task ULong()
    {
        return Test<ulong>("ULong", 8, 9);
    }

    [Fact]
    public virtual Task ByteArray()
    {
        return Test<byte[]>(
            "ByteArray",
            [0x00, 0x01, 0xFF],
            [0x0A, 0x0B],
            false);
    }

    [Fact]
    public virtual Task String_list()
    {
        return Test<List<string>>(
            "StringList",
            ["foo", "bar"],
            ["foo", "baz"],
            false);
    }

    protected override object? GenerateEmptyProperty(VectorStoreProperty property)
    {
        return property.Type switch
        {
            var t when t == typeof(DateTime) => new DateTime(1000, 1, 1),
            var t when t == typeof(DateTimeOffset) => new DateTimeOffset(1000, 1, 1, 0, 0, 0, TimeSpan.Zero),
#if NET
            var t when t == typeof(DateOnly) => new DateOnly(1000, 1, 1),
#endif
            var t when t == typeof(List<string>) => new List<string>(),
            _ => base.GenerateEmptyProperty(property)
        };
    }

    public class Record : DefaultRecord
    {
        public sbyte SByte { get; set; }
        public ushort UShort { get; set; }
        public uint UInt { get; set; }
        public ulong ULong { get; set; }
        public byte[] ByteArray { get; set; } = null!;
        public List<string> StringList { get; set; } = null!;
    }

    public new class Fixture : DataTypeTests<Guid, Record>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;

        public override IList<VectorStoreDataProperty> GetDataProperties()
        {
            var properties = base.GetDataProperties();
            properties.Add(new VectorStoreDataProperty(nameof(Record.SByte), typeof(sbyte)) { IsIndexed = true });
            properties.Add(new VectorStoreDataProperty(nameof(Record.UShort), typeof(ushort)) { IsIndexed = true });
            properties.Add(new VectorStoreDataProperty(nameof(Record.UInt), typeof(uint)) { IsIndexed = true });
            properties.Add(new VectorStoreDataProperty(nameof(Record.ULong), typeof(ulong)) { IsIndexed = true });
            properties.Add(new VectorStoreDataProperty(nameof(Record.ByteArray), typeof(byte[])));
            properties.Add(new VectorStoreDataProperty(nameof(Record.StringList), typeof(List<string>)) { IsIndexed = true });
            return properties;
        }
    }
}
