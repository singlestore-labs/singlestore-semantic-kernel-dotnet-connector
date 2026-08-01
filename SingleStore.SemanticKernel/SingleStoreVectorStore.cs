using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
///     Represents a vector store implementation using SingleStore.
/// </summary>
public sealed class SingleStoreVectorStore : VectorStore
{
    /// <summary>Data source used to interact with the database.</summary>
    private readonly SingleStoreDataSource _dataSource;

    private readonly SingleStoreDataSourceArc? _dataSourceArc;
    private readonly string _databaseName;

    private readonly IEmbeddingGenerator? _embeddingGenerator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SingleStoreVectorStore" /> class.
    /// </summary>
    /// <param name="dataSource">SingleStore data source.</param>
    /// <param name="ownsDataSource">
    ///     A value indicating whether <paramref name="dataSource" /> is disposed when this instance
    ///     of <see cref="SingleStoreVectorStore" /> is disposed.
    /// </param>
    /// <param name="options">Optional configuration options for this class</param>
    public SingleStoreVectorStore(SingleStoreDataSource dataSource,
        bool ownsDataSource,
        SingleStoreVectorStoreOptions? options = default)
    {
        Verify.NotNull(dataSource);

        _embeddingGenerator = options?.EmbeddingGenerator;
        _dataSource = dataSource;
        _dataSourceArc = ownsDataSource ? new SingleStoreDataSourceArc(dataSource) : null;
        _databaseName = new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).Database!;

        // Don't add any lines after this - an exception thrown afterward would leave the reference count wrongly incremented.
        _dataSourceArc?.IncrementReferenceCount();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SingleStoreVectorStore" /> class.
    /// </summary>
    /// <param name="connectionString">SingleStore database connection string.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public SingleStoreVectorStore(string connectionString, SingleStoreVectorStoreOptions? options = default) : this(
        new SingleStoreDataSource(connectionString),
        true,
        options)
    {
    }

    /// <inheritdoc />
    [RequiresDynamicCode(
        "This API is not compatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, use GetDynamicCollection() instead.")]
    [RequiresUnreferencedCode(
        "This API is not compatible with trimming. For dynamic mapping via Dictionary<string, object?>, use GetDynamicCollection() instead.")]
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name,
        VectorStoreCollectionDefinition definition)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _dataSourceArc?.Dispose();
        base.Dispose(disposing);
    }
}
