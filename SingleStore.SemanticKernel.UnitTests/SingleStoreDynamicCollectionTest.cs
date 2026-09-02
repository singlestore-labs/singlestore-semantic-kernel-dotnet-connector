using System;
using Microsoft.Extensions.VectorData;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreDynamicCollectionTest
{
    private const string TestConnectionString = "Host=localhost;Database=test;";
    private const string TestDataSourceConnectionString = "Host=localhost;Database=test;AllowLoadLocalInfile=True;";
    private const string TestCollectionName = "testcollection";

    private static readonly SingleStoreCollectionOptions ValidOptions = new()
    {
        Definition = new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty("id", typeof(Guid)),
                new VectorStoreDataProperty("name", typeof(string)),
                new VectorStoreVectorProperty("embedding", typeof(ReadOnlyMemory<float>), 10)
            ]
        }
    };

    [Fact]
    public void ThrowsForUnsupportedDataType()
    {
        var options = new SingleStoreCollectionOptions
        {
            Definition = new VectorStoreCollectionDefinition
            {
                Properties =
                [
                    new VectorStoreKeyProperty("id", typeof(Guid)),
                    new VectorStoreDataProperty("uri", typeof(Uri))
                ]
            }
        };

        Assert.Throws<NotSupportedException>(() =>
            new SingleStoreDynamicCollection(TestConnectionString, TestCollectionName, options));
    }

    [Fact]
    public void ThrowsWhenDefinitionIsMissing()
    {
        var options = new SingleStoreCollectionOptions();

        var exception = Assert.Throws<ArgumentException>(() =>
            new SingleStoreDynamicCollection(TestConnectionString, TestCollectionName, options));

        Assert.Contains("Definition is required for dynamic collections", exception.Message);
    }

    [Fact]
    public void WhenOwnsDataSourceAndConstructionFails_DisposesDataSource()
    {
        var dataSource = new SingleStoreDataSource(TestDataSourceConnectionString);
        var options = new SingleStoreCollectionOptions();

        Assert.Throws<ArgumentException>(() =>
            new SingleStoreDynamicCollection(dataSource, TestCollectionName, true, options));

        Assert.ThrowsAny<ObjectDisposedException>(() => dataSource.CreateConnection());
    }

    [Fact]
    public void WhenDoesNotOwnDataSourceAndConstructionFails_DoesNotDisposeDataSource()
    {
        using var dataSource = new SingleStoreDataSource(TestDataSourceConnectionString);
        var options = new SingleStoreCollectionOptions();

        Assert.Throws<ArgumentException>(() =>
            new SingleStoreDynamicCollection(dataSource, TestCollectionName, false, options));

        using var connection = dataSource.CreateConnection();
        Assert.NotNull(connection);
    }

    [Fact]
    public void CanConstructFromConnectionString()
    {
        using var collection = new SingleStoreDynamicCollection(
            TestConnectionString,
            TestCollectionName,
            ValidOptions);

        Assert.Equal(TestCollectionName, collection.Name);
    }

    [Fact]
    public void CanConstructFromDataSource()
    {
        using var dataSource = new SingleStoreDataSource(TestDataSourceConnectionString);
        using var collection = new SingleStoreDynamicCollection(
            dataSource,
            TestCollectionName,
            false,
            ValidOptions);

        Assert.Equal(TestCollectionName, collection.Name);
    }

    [Fact]
    public void Dispose_WhenOwnsDataSource_DisposesDataSource()
    {
        var dataSource = new SingleStoreDataSource(TestDataSourceConnectionString);
        var collection = new SingleStoreDynamicCollection(
            dataSource,
            TestCollectionName,
            true,
            ValidOptions);

        collection.Dispose();

        Assert.ThrowsAny<ObjectDisposedException>(() => dataSource.CreateConnection());
    }

    [Fact]
    public void Dispose_WhenDoesNotOwnDataSource_DoesNotDisposeDataSource()
    {
        using var dataSource = new SingleStoreDataSource(TestDataSourceConnectionString);
        var collection = new SingleStoreDynamicCollection(
            dataSource,
            TestCollectionName,
            false,
            ValidOptions);

        collection.Dispose();

        using var connection = dataSource.CreateConnection();
        Assert.NotNull(connection);
    }
}
