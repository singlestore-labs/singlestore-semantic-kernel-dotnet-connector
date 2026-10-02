using System;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreUtilTests
{
    [Fact]
    public void CreateDataSource_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SingleStoreUtil.CreateDataSource(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDataSource_WhiteSpace_Throws(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => SingleStoreUtil.CreateDataSource(connectionString));
    }

    [Fact]
    public void CreateDataSource_WhenAbsent_SetsAllowLoadLocalInfile()
    {
        using var dataSource = SingleStoreUtil.CreateDataSource("Host=localhost;Database=testdb;");

        Assert.True(new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).AllowLoadLocalInfile);
    }

    [Fact]
    public void CreateDataSource_WhenFalse_LeavesFlagUnchanged()
    {
        using var dataSource = SingleStoreUtil.CreateDataSource(
            "Host=localhost;Database=testdb;AllowLoadLocalInfile=false;");

        Assert.False(new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).AllowLoadLocalInfile);
    }

    [Fact]
    public void CreateDataSource_WhenTrue_LeavesFlagEnabled()
    {
        using var dataSource = SingleStoreUtil.CreateDataSource(
            "Host=localhost;Database=testdb;AllowLoadLocalInfile=true;");

        Assert.True(new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).AllowLoadLocalInfile);
    }

    [Fact]
    public void CreateDataSource_WhenAbsent_SetsConnectionAttributes()
    {
        using var dataSource = SingleStoreUtil.CreateDataSource("Host=localhost;Database=testdb;");

        var connectorVersion = typeof(SingleStoreUtil).Assembly.GetName().Version!.ToString(3);
        var attributes = new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).ConnectionAttributes;

        Assert.Equal(
            $"_connector_name:SingleStore Semantic Kernel .NET Connector,_connector_version:{connectorVersion},_product_version:10.8.0",
            attributes);
    }

    [Fact]
    public void CreateDataSource_WhenPresent_LeavesConnectionAttributesUnchanged()
    {
        const string connectionAttributes = "_connector_name:custom,_connector_version:0.1.0,_product_version:9.0.0";

        using var dataSource = SingleStoreUtil.CreateDataSource(
            $"Host=localhost;Database=testdb;ConnectionAttributes={connectionAttributes};");

        Assert.Equal(connectionAttributes, new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).ConnectionAttributes);
    }

    [Fact]
    public void CreateDataSource_WhenPartial_AppendsMissingConnectionAttributes()
    {
        using var dataSource = SingleStoreUtil.CreateDataSource(
            "Host=localhost;Database=testdb;ConnectionAttributes=program_name:myapp");

        var connectorVersion = typeof(SingleStoreUtil).Assembly.GetName().Version!.ToString(3);
        var attributes = new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).ConnectionAttributes;

        Assert.Equal(
            $"program_name:myapp,_connector_name:SingleStore Semantic Kernel .NET Connector,_connector_version:{connectorVersion},_product_version:10.8.0",
            attributes);
    }
}
