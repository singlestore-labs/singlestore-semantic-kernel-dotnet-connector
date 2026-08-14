using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreModelBuilderTest
{
    [Theory]
    [InlineData(typeof(short))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(string))]
    [InlineData(typeof(Guid))]
    public void BuildDynamic_SupportedKeyType_Succeeds(Type keyType)
    {
        var model = BuildDynamic(
            new VectorStoreKeyProperty("id", keyType),
            new VectorStoreDataProperty("name", typeof(string)));

        Assert.Equal(keyType, model.KeyProperty.Type);
        Assert.Equal("id", model.KeyProperty.ModelName);
    }

    [Fact]
    public void BuildDynamic_UnsupportedKeyType_Throws()
    {
        Assert.Throws<NotSupportedException>(() => BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(byte)),
            new VectorStoreDataProperty("name", typeof(string))));
    }

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(int))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(string))]
    [InlineData(typeof(byte[]))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(long?))]
#if NET
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(TimeOnly))]
    [InlineData(typeof(DateOnly?))]
    [InlineData(typeof(TimeOnly?))]
#endif
    public void BuildDynamic_SupportedDataType_Succeeds(Type dataType)
    {
        var model = BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(Guid)),
            new VectorStoreDataProperty("value", dataType));

        Assert.Single(model.DataProperties);
        Assert.Equal(dataType, model.DataProperties[0].Type);
    }

    [Fact]
    public void BuildDynamic_UnsupportedDataType_Throws()
    {
        Assert.Throws<NotSupportedException>(() => BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(Guid)),
            new VectorStoreDataProperty("value", typeof(object))));
    }

    [Theory]
    [InlineData(typeof(ReadOnlyMemory<sbyte>))]
    [InlineData(typeof(ReadOnlyMemory<short>))]
    [InlineData(typeof(ReadOnlyMemory<int>))]
    [InlineData(typeof(ReadOnlyMemory<long>))]
    [InlineData(typeof(ReadOnlyMemory<float>))]
    [InlineData(typeof(ReadOnlyMemory<double>))]
    [InlineData(typeof(double[]))]
    [InlineData(typeof(sbyte[]))]
    [InlineData(typeof(short[]))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(long[]))]
    [InlineData(typeof(float[]))]
    [InlineData(typeof(Embedding<sbyte>))]
    [InlineData(typeof(Embedding<short>))]
    [InlineData(typeof(Embedding<int>))]
    [InlineData(typeof(Embedding<long>))]
    [InlineData(typeof(Embedding<float>))]
    [InlineData(typeof(Embedding<double>))]
    public void BuildDynamic_SupportedVectorType_Succeeds(Type vectorType)
    {
        var model = BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(Guid)),
            new VectorStoreVectorProperty("embedding", vectorType, 10));

        Assert.Single(model.VectorProperties);
        Assert.Equal(vectorType, model.VectorProperties[0].Type);
        Assert.Equal(10, model.VectorProperties[0].Dimensions);
    }

    [Fact]
    public void BuildDynamic_UnsupportedVectorType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(Guid)),
            new VectorStoreVectorProperty("embedding", typeof(byte[]), 10)));
    }

    [Fact]
    public void BuildDynamic_NoVectorProperties_Succeeds()
    {
        var model = BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(Guid)),
            new VectorStoreDataProperty("name", typeof(string)));

        Assert.Empty(model.VectorProperties);
        Assert.Single(model.DataProperties);
    }

    [Fact]
    public void BuildDynamic_MultipleVectorProperties_Succeeds()
    {
        var model = BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(Guid)),
            new VectorStoreVectorProperty("embedding1", typeof(ReadOnlyMemory<float>), 10),
            new VectorStoreVectorProperty("embedding2", typeof(ReadOnlyMemory<double>), 20));

        Assert.Equal(2, model.VectorProperties.Count);
        Assert.Equal("embedding1", model.VectorProperties[0].ModelName);
        Assert.Equal("embedding2", model.VectorProperties[1].ModelName);
    }

    [Fact]
    public void BuildDynamic_LongKey_AutoGenerated_Succeeds()
    {
        var model = BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(long)) { IsAutoGenerated = true },
            new VectorStoreDataProperty("name", typeof(string)));

        Assert.True(model.KeyProperty.IsAutoGenerated);
    }

    [Fact]
    public void BuildDynamic_NonLongKey_AutoGenerated_Throws()
    {
        Assert.Throws<NotSupportedException>(() => BuildDynamic(
            new VectorStoreKeyProperty("id", typeof(short)) { IsAutoGenerated = true },
            new VectorStoreDataProperty("name", typeof(string))));
    }

    private static CollectionModel BuildDynamic(params VectorStoreProperty[] properties)
    {
        var definition = new VectorStoreCollectionDefinition { Properties = properties };
        return new SingleStoreModelBuilder().BuildDynamic(definition, null);
    }
}
