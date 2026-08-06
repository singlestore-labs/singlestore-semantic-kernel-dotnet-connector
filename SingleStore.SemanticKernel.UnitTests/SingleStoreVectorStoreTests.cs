using System;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreVectorStoreTests
{
    [Fact]
    public void CreateVectorStore_WithNullDataSource_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SingleStoreVectorStore(null!, false));

        Assert.Equal("dataSource", exception.ParamName);
    }
}
