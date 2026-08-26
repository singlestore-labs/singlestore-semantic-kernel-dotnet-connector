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

    public override string DefaultDistanceFunction => DistanceFunction.DotProductSimilarity;


    public SingleStoreDataSource DataSource => _dataSource ?? throw new InvalidOperationException("Not initialized");

    public static SingleStoreTestStore Instance { get; } = new();

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

        CreateDatabase(connectionStringBuilder);
        connectionStringBuilder.Database = DefaultDatabase;
        _connectionString = connectionStringBuilder.ConnectionString;

        SingleStoreDataSourceBuilder dataSourceBuilder = new(_connectionString!);
        _dataSource = dataSourceBuilder.Build();

        // It's a shared static instance, we don't want any of the tests to dispose it.
        DefaultVectorStore = new SingleStoreVectorStore(_dataSource, false);
    }

    private void CreateDatabase(SingleStoreConnectionStringBuilder connectionStringBuilder)
    {
        using var conn = new SingleStoreConnection(connectionStringBuilder.ConnectionString);
        conn.Open();

        using var command = new SingleStoreCommand($"DROP DATABASE IF EXISTS {DefaultDatabase}", conn);
        command.ExecuteNonQuery();
        command.CommandText = $"CREATE DATABASE {DefaultDatabase}";
        command.ExecuteNonQuery();
    }

    protected override async Task StopAsync()
    {
        if (_dataSource is not null) await _dataSource.DisposeAsync();

        // Only stop the container if we started it
        if (!_useExternalInstance) await Container.StopAsync();
    }
}
