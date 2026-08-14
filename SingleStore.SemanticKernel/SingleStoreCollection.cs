using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Represents a collection of vector store records in a SingleStore database.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TRecord">The type of the record.</typeparam>
public class SingleStoreCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>, IKeywordHybridSearchable<TRecord>
    where TKey : notnull
    where TRecord : class
{
    /// <summary>Data source used to interact with the database.</summary>
    private readonly SingleStoreDataSource _dataSource;

    private readonly SingleStoreDataSourceArc? _dataSourceArc;
    private readonly string _databaseName;

    /// <summary>The model for this collection.</summary>
    private readonly CollectionModel _model;


    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreCollection{TKey, TRecord}" /> class.
    /// </summary>
    /// <param name="dataSource">The data source to use for connecting to the database.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="ownsDataSource">A value indicating whether <paramref name="dataSource" /> is disposed when the collection is disposed. Ownership transfers immediately, so <paramref name="dataSource" /> is also disposed if this constructor throws.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    [RequiresDynamicCode(
        "This constructor is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, instantiate SingleStoreDynamicCollection instead.")]
    [RequiresUnreferencedCode(
        "This constructor is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, instantiate SingleStoreDynamicCollection instead")]
    public SingleStoreCollection(SingleStoreDataSource dataSource,
        string name,
        bool ownsDataSource,
        SingleStoreCollectionOptions? options = default) : this(dataSource,
        ownsDataSource ? new SingleStoreDataSourceArc(dataSource) : null,
        ownsDataSource,
        name,
        options)
    {
    }

    [RequiresDynamicCode(
        "This constructor is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, instantiate SingleStoreDynamicCollection instead.")]
    [RequiresUnreferencedCode(
        "This constructor is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, instantiate SingleStoreDynamicCollection instead.")]
    internal SingleStoreCollection(SingleStoreDataSource dataSource,
        SingleStoreDataSourceArc? dataSourceArc,
        bool ownsDataSource,
        string name,
        SingleStoreCollectionOptions? options)
        : this(dataSource,
            dataSourceArc,
            ownsDataSource,
            name,
            static options => typeof(TRecord) == typeof(Dictionary<string, object?>)
                ? throw new NotSupportedException(
                    VectorDataStrings.NonDynamicCollectionWithDictionaryNotSupported(
                        typeof(SingleStoreDynamicCollection)))
                : new SingleStoreModelBuilder().Build(typeof(TRecord),
                    typeof(TKey),
                    options.Definition,
                    options.EmbeddingGenerator),
            options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreCollection{TKey, TRecord}" /> class.
    /// </summary>
    /// <param name="connectionString">SingleStore database connection string.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    [RequiresDynamicCode(
        "This constructor is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, instantiate SingleStoreDynamicCollection instead.")]
    [RequiresUnreferencedCode(
        "This constructor is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, instantiate SingleStoreDynamicCollection instead")]
    public SingleStoreCollection(string connectionString, string name, SingleStoreCollectionOptions? options = default)
        : this(new SingleStoreDataSource(connectionString), name, true, options)
    {
    }

    internal SingleStoreCollection(SingleStoreDataSource dataSource,
        SingleStoreDataSourceArc? dataSourceArc,
        bool ownsDataSource,
        string name,
        Func<SingleStoreCollectionOptions, CollectionModel> modelFactory,
        SingleStoreCollectionOptions? options)
    {
        Verify.NotNull(dataSource);

        try
        {
            Verify.NotNullOrWhiteSpace(name);

            options ??= SingleStoreCollectionOptions.Default;

            Name = name;
            _model = modelFactory(options);

            _dataSource = dataSource;
            _dataSourceArc = dataSourceArc;
            _databaseName = new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).Database!;

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

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc cref="VectorStoreCollection{TKey, TRecord}.GetService(Type, object?)" />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<VectorSearchResult<TRecord>> HybridSearchAsync<TInput>(TInput searchValue,
        ICollection<string> keywords,
        int top,
        HybridSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default) where TInput : notnull
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<TRecord?> GetAsync(TKey key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<TRecord> GetAsync(IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<TRecord> GetAsync(Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
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
