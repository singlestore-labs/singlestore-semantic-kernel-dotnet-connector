using System;
using Microsoft.Extensions.VectorData;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreCollectionTests
{
    private const string TestConnectionString = "Host=localhost;Database=testdb;";
    private const string TestDataSourceConnectionString = "Host=localhost;Database=testdb;AllowLoadLocalInfile=True;";
    private const string TestCollectionName = "testcollection";

    private readonly SingleStoreCollection<string, SingleStoreHotel<string>> _collection =
        new(TestConnectionString, TestCollectionName);

    [Fact]
    public void GetService_NullServiceType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _collection.GetService(null!));
    }

    [Fact]
    public void GetService_WithUnsupportedServiceKey_ReturnsNull()
    {
        Assert.Null(_collection.GetService(typeof(VectorStoreCollectionMetadata), "key"));
    }

    [Fact]
    public void GetService_VectorStoreCollectionMetadata_ReturnsMetadata()
    {
        var metadata = _collection.GetService(typeof(VectorStoreCollectionMetadata)) as VectorStoreCollectionMetadata;

        Assert.NotNull(metadata);
        Assert.Equal(SingleStoreConstants.VectorStoreSystemName, metadata.VectorStoreSystemName);
        Assert.Equal("testdb", metadata.VectorStoreName);
        Assert.Equal(TestCollectionName, metadata.CollectionName);
    }

    [Fact]
    public void GetService_SingleStoreDataSource_ReturnsDataSource()
    {
        using var dataSource = new SingleStoreDataSource(TestDataSourceConnectionString);
        using var dataSourceCollection = new SingleStoreCollection<string, SingleStoreHotel<string>>(
            dataSource,
            TestCollectionName,
            false);

        var result = dataSourceCollection.GetService(typeof(SingleStoreDataSource));

        Assert.Same(dataSource, result);
    }

    [Fact]
    public void GetService_CollectionTypes_ReturnsSelf()
    {
        Assert.Same(_collection,
            _collection.GetService(typeof(SingleStoreCollection<string, SingleStoreHotel<string>>)));
    }

    [Fact]
    public void GetService_UnknownType_ReturnsNull()
    {
        Assert.Null(_collection.GetService(typeof(string)));
    }

    [Fact]
    public void ConnectionStringConstructor_EnablesAllowLoadLocalInfile()
    {
        using var collection = new SingleStoreCollection<string, SingleStoreHotel<string>>(
            TestConnectionString,
            TestCollectionName);

        var dataSource = Assert.IsType<SingleStoreDataSource>(collection.GetService(typeof(SingleStoreDataSource)));
        Assert.True(new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).AllowLoadLocalInfile);
    }

    [Fact]
    public void DataSourceConstructor_WithoutAllowLoadLocalInfile_Throws()
    {
        using var dataSource = new SingleStoreDataSource(TestConnectionString);

        var exception = Assert.Throws<ArgumentException>(() =>
            new SingleStoreCollection<string, SingleStoreHotel<string>>(dataSource, TestCollectionName, false));

        Assert.Equal("dataSource", exception.ParamName);
        Assert.Contains("AllowLoadLocalInfile", exception.Message);
    }
}
