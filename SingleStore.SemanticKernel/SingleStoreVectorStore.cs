using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Represents a vector store implementation using SingleStore.
/// </summary>
public sealed class SingleStoreVectorStore : VectorStore
{
    /// <summary>A general purpose definition that can be used to construct a collection when needing to proxy schema agnostic operations.</summary>
    private static readonly VectorStoreCollectionDefinition GeneralPurposeDefinition = new() { Properties = [new VectorStoreKeyProperty("Key", typeof(string))] };

    /// <summary>Data source used to interact with the database.</summary>
    private readonly SingleStoreDataSource _dataSource;


    private readonly SingleStoreDataSourceArc? _dataSourceArc;
    private readonly string _databaseName;

    private readonly IEmbeddingGenerator? _embeddingGenerator;

    /// <summary>Metadata about vector store.</summary>
    private readonly VectorStoreMetadata _metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreVectorStore" /> class.
    /// </summary>
    /// <param name="dataSource">SingleStore data source.</param>
    /// <param name="ownsDataSource">
    /// A value indicating whether <paramref name="dataSource" /> is disposed when this instance
    /// of <see cref="SingleStoreVectorStore" /> is disposed. Ownership transfers immediately, so
    /// <paramref name="dataSource" /> is also disposed if this constructor throws.
    /// </param>
    /// <param name="options">Optional configuration options for this class</param>
    public SingleStoreVectorStore(SingleStoreDataSource dataSource,
        bool ownsDataSource,
        SingleStoreVectorStoreOptions? options = default)
    {
        Verify.NotNull(dataSource);

        try
        {
            _embeddingGenerator = options?.EmbeddingGenerator;
            _dataSource = dataSource;
            _dataSourceArc = ownsDataSource ? new SingleStoreDataSourceArc(dataSource) : null;
            _databaseName = new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).Database!;
            _metadata = new VectorStoreMetadata
            {
                VectorStoreSystemName = SingleStoreConstants.VectorStoreSystemName,
                VectorStoreName = _databaseName
            };

            // Don't add any lines after this - an exception thrown afterward would leave the reference count wrongly incremented.
            _dataSourceArc?.IncrementReferenceCount();
        }
        catch when (ownsDataSource)
        {
            // We own the data source, so nobody else will dispose it once construction fails.
            dataSource.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreVectorStore" /> class.
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
#if NET
    public override SingleStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name,
        VectorStoreCollectionDefinition? definition = null)
#else
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name,
        VectorStoreCollectionDefinition? definition = null)
#endif
    {
        if (typeof(TRecord) == typeof(Dictionary<string, object?>))
            throw new ArgumentException(VectorDataStrings.GetCollectionWithDictionaryNotSupported);

        return new SingleStoreCollection<TKey, TRecord>(
            _dataSource,
            _dataSourceArc,
            false,
            name,
            new SingleStoreCollectionOptions
            {
                Definition = definition,
                EmbeddingGenerator = _embeddingGenerator
            });
    }

    /// <inheritdoc />
#if NET
    public override SingleStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name,
        VectorStoreCollectionDefinition definition)
#else
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name,
        VectorStoreCollectionDefinition definition)
#endif
    {
        return new SingleStoreDynamicCollection(
            _dataSource,
            _dataSourceArc,
            false,
            name,
            new SingleStoreCollectionOptions
            {
                Definition = definition,
                EmbeddingGenerator = _embeddingGenerator
            }
        );
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.ShowTables(connection, _databaseName);

        using var reader = await connection.ExecuteWithErrorHandlingAsync(
            _metadata,
            "ListCollectionNames",
            () => command.ExecuteReaderAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadWithErrorHandlingAsync(
                   _metadata,
                   "ListCollectionNames",
                   cancellationToken).ConfigureAwait(false))
        {
            yield return reader.GetString(0);
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        using var collection = GetDynamicCollection(name, GeneralPurposeDefinition);
        return await collection.CollectionExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        using var collection = GetDynamicCollection(name, GeneralPurposeDefinition);
        await collection.EnsureCollectionDeletedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Verify.NotNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreMetadata) ? _metadata :
            serviceType == typeof(SingleStoreDataSource) ? _dataSource :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _dataSourceArc?.Dispose();
        base.Dispose(disposing);
    }
}
