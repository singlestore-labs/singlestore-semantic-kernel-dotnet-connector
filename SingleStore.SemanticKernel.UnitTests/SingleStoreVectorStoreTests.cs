using System;
using Microsoft.Extensions.VectorData;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreVectorStoreTests
{
    private const string TestConnectionString = "Host=localhost;Database=testdb;";
    private readonly SingleStoreVectorStore _store = new(TestConnectionString);

    [Fact]
    public void CreateVectorStore_WithNullDataSource_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SingleStoreVectorStore(null!, false));

        Assert.Equal("dataSource", exception.ParamName);
    }

    [Fact]
    public void GetService_NullServiceType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _store.GetService(null!));
    }

    [Fact]
    public void GetService_WithServiceKey_ReturnsNull()
    {
        Assert.Null(_store.GetService(typeof(VectorStoreMetadata), "key"));
    }

    [Fact]
    public void GetService_VectorStoreMetadata_ReturnsMetadata()
    {
        var metadata = _store.GetService(typeof(VectorStoreMetadata)) as VectorStoreMetadata;

        Assert.NotNull(metadata);
        Assert.Equal(SingleStoreConstants.VectorStoreSystemName, metadata.VectorStoreSystemName);
        Assert.Equal("testdb", metadata.VectorStoreName);
    }

    [Fact]
    public void GetService_SingleStoreDataSource_ReturnsDataSource()
    {
        using var dataSource = new SingleStoreDataSource(TestConnectionString);
        using var dataSourceStore = new SingleStoreVectorStore(dataSource, false);

        var result = dataSourceStore.GetService(typeof(SingleStoreDataSource));

        Assert.Same(dataSource, result);
    }

    [Fact]
    public void GetService_StoreTypes_ReturnsSelf()
    {
        Assert.Same(_store, _store.GetService(typeof(SingleStoreVectorStore)));
    }

    [Fact]
    public void GetService_UnknownType_ReturnsNull()
    {
        Assert.Null(_store.GetService(typeof(string)));
    }

    [Fact]
    public void ConnectionStringConstructor_EnablesAllowLoadLocalInfile()
    {
        using var store = new SingleStoreVectorStore(TestConnectionString);

        var dataSource = Assert.IsType<SingleStoreDataSource>(store.GetService(typeof(SingleStoreDataSource)));
        Assert.True(new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).AllowLoadLocalInfile);
    }
}
