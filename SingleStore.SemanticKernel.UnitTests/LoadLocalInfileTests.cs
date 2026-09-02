using System;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class LoadLocalInfileTests
{
    [Fact]
    public void Enable_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LoadLocalInfile.Enable(null!));
    }

    [Fact]
    public void Enable_WhenAbsent_SetsFlag()
    {
        var result = LoadLocalInfile.Enable("Host=localhost;Database=testdb;");

        Assert.True(new SingleStoreConnectionStringBuilder(result).AllowLoadLocalInfile);
    }

    [Fact]
    public void Enable_WhenFalse_OverridesToTrue()
    {
        var result = LoadLocalInfile.Enable("Host=localhost;Database=testdb;AllowLoadLocalInfile=false;");

        Assert.True(new SingleStoreConnectionStringBuilder(result).AllowLoadLocalInfile);
    }

    [Fact]
    public void Require_WhenEnabled_DoesNotThrow()
    {
        using var dataSource = new SingleStoreDataSource("Host=localhost;Database=testdb;AllowLoadLocalInfile=true;");

        LoadLocalInfile.Require(dataSource);
    }

    [Fact]
    public void Require_WhenMissing_Throws()
    {
        using var dataSource = new SingleStoreDataSource("Host=localhost;Database=testdb;");

        var exception = Assert.Throws<ArgumentException>(() => LoadLocalInfile.Require(dataSource));

        Assert.Equal("dataSource", exception.ParamName);
        Assert.Contains("AllowLoadLocalInfile", exception.Message);
    }
}
