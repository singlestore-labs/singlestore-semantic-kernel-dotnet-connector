using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

internal static class LoadLocalInfile
{
    internal static string Enable(string connectionString)
    {
        Verify.NotNull(connectionString);

        var builder = new SingleStoreConnectionStringBuilder(connectionString)
        {
            AllowLoadLocalInfile = true
        };
        return builder.ConnectionString;
    }

    internal static void Require(SingleStoreDataSource dataSource)
    {
        if (new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).AllowLoadLocalInfile)
        {
            return;
        }

        throw new ArgumentException(
            "The SingleStore data source must have AllowLoadLocalInfile=true in its connection string, because upserts use LOAD DATA LOCAL INFILE.",
            nameof(dataSource));
    }
}
