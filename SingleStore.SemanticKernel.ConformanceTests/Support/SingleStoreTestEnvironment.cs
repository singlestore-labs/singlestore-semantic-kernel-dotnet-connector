using Microsoft.Extensions.Configuration;

namespace SingleStore.SemanticKernel.ConformanceTests.Support;

internal static class SingleStoreTestEnvironment
{
    public static readonly string? ConnectionString;

    static SingleStoreTestEnvironment()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var singlestoreSection = configuration.GetSection("SingleStore");
        ConnectionString = singlestoreSection["ConnectionString"];
    }

    public static bool IsConnectionStringDefined => ConnectionString is not null;
}
