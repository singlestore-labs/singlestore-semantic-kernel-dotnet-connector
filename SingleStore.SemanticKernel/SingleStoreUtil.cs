using Microsoft.Extensions.VectorData;
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

        if (!builder.ConnectionAttributes.Contains("_connector_name"))
        {
            if (builder.ConnectionAttributes.Length > 0)
            {
                builder.ConnectionAttributes += ",";
            }

            var semanticKernelVersion = typeof(VectorStore).Assembly.GetName().Version!.ToString(3);
            var singlestoreConnectorVersion = typeof(SingleStoreUtil).Assembly.GetName().Version!.ToString(3);
            builder.ConnectionAttributes += $"_connector_name:SingleStore Semantic Kernel .NET Connector,_connector_version:{singlestoreConnectorVersion},_product_version:{semanticKernelVersion}";
        }

        return new SingleStoreDataSource(builder.ConnectionString);
    }
}
