using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

internal static class SingleStoreUtil
{
    internal static SingleStoreDataSource CreateDataSource(string connectionString)
    {
        Verify.NotNullOrWhiteSpace(connectionString);

        var builder = new SingleStoreConnectionStringBuilder(connectionString);
        if (!builder.ContainsKey("AllowLoadLocalInfile"))
        {
            builder.AllowLoadLocalInfile = true;
        }

        return new SingleStoreDataSource(builder.ConnectionString);
    }
}
