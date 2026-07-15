using SingleStoreConnector;
using Xunit.Abstractions;

namespace SingleStore.SemanticKernel.IntegrationTests;

[Collection("Database collection")]
public class UnitTest1
{
    private readonly DatabaseFixture _databaseFixture;
    ITestOutputHelper _testOutputHelper;

    public UnitTest1(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
    {
        _databaseFixture = databaseFixture;
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Test1()
    {
        using SingleStoreDataReader reader = _databaseFixture.ExecuteReader("SELECT 1");
        Assert.True(reader.Read());
        Assert.Equal(1L, reader[0]);
        Assert.False(reader.Read());
    }
}