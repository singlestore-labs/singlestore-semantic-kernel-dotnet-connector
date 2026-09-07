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
}
