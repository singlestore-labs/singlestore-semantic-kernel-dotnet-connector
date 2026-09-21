using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.VectorData;
using SingleStoreConnector;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests.Support;

public class SingleStoreTestStore : TestStore
{
    private const ushort DefaultContainerPort = 3306;
    private const string DefaultUserId = "root";
    private const string DefaultDatabase = "testdb";
    private static readonly string DefaultRootPassword = Guid.NewGuid().ToString("N");

    private static readonly IContainer Container =
        new ContainerBuilder("ghcr.io/singlestore-labs/singlestoredb-dev:latest")
            .WithEnvironment("ROOT_PASSWORD", DefaultRootPassword).WithPortBinding(DefaultContainerPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy()).Build();

    private string? _connectionString;

    private SingleStoreDataSource? _dataSource;
    private bool _useExternalInstance;

    private SingleStoreTestStore()
    {
    }

    public override string DefaultDistanceFunction => DistanceFunction.EuclideanDistance;

    public SingleStoreDataSource DataSource => _dataSource ?? throw new InvalidOperationException("Not initialized");

    public static SingleStoreTestStore Instance { get; } = new();

    public Version ServerVersion { get; private set; } = new(0, 0);

    public bool SupportsMultiValueHashIndex => ServerVersion.Major >= 9;

    public SingleStoreVectorStore GetVectorStore(SingleStoreVectorStoreOptions options)
    {
        // The DataSource is shared with the static instance, we don't want any of the tests to dispose it.
        return new SingleStoreVectorStore(DataSource, false, options);
    }

    protected override async Task StartAsync()
    {
        SingleStoreConnectionStringBuilder connectionStringBuilder;

        // Determine connection string source
        if (SingleStoreTestEnvironment.IsConnectionStringDefined)
        {
            connectionStringBuilder =
                new SingleStoreConnectionStringBuilder(SingleStoreTestEnvironment.ConnectionString!);
            _useExternalInstance = true;
        }
        else
        {
            // Use testcontainer if no external connection string is provided
            await Container.StartAsync();

            connectionStringBuilder = new SingleStoreConnectionStringBuilder
            {
                Server = Container.Hostname,
                Port = Container.GetMappedPublicPort(DefaultContainerPort),
                UserID = DefaultUserId,
                Password = DefaultRootPassword
            };
            _useExternalInstance = false;
        }

        PrepareDatabase(connectionStringBuilder);
        connectionStringBuilder.Database = DefaultDatabase;
        connectionStringBuilder.AllowLoadLocalInfile = true;
        _connectionString = connectionStringBuilder.ConnectionString;

        SingleStoreDataSourceBuilder dataSourceBuilder = new(_connectionString!);
        _dataSource = dataSourceBuilder.Build();

        // It's a shared static instance, we don't want any of the tests to dispose it.
        DefaultVectorStore = new SingleStoreVectorStore(_dataSource, false);
    }

    private void PrepareDatabase(SingleStoreConnectionStringBuilder connectionStringBuilder)
    {
        using var conn = new SingleStoreConnection(connectionStringBuilder.ConnectionString);
        conn.Open();

        using var command = new SingleStoreCommand("SELECT @@memsql_version", conn);
        var rawVersion = (string)command.ExecuteScalar()!;
        ServerVersion = Version.Parse(rawVersion.Split('-', '+')[0]);

        // Workaround for ECS-3820
        command.CommandText = "SET GLOBAL vector_index_fallback_non_index_scan = false";
        command.ExecuteNonQuery();

        command.CommandText = $"DROP DATABASE IF EXISTS {DefaultDatabase}";
        command.ExecuteNonQuery();
        command.CommandText = $"CREATE DATABASE {DefaultDatabase}";
        command.ExecuteNonQuery();
    }

    internal void DisableUnsupportedJsonIndexes(IEnumerable<VectorStoreProperty> properties)
    {
        if (SupportsMultiValueHashIndex)
        {
            return;
        }

        foreach (var property in properties)
        {
            if (property is VectorStoreDataProperty dataProperty
                && (dataProperty.Type == typeof(string[]) || dataProperty.Type == typeof(List<string>)))
            {
                dataProperty.IsIndexed = false;
            }
        }
    }

    protected override async Task StopAsync()
    {
        if (_dataSource is not null) await _dataSource.DisposeAsync();

        // Only stop the container if we started it
        if (!_useExternalInstance) await Container.StopAsync();
    }
}
