using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq.Expressions;
using Microsoft.Extensions.AI;
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
    /// <summary>The default options for vector search.</summary>
    private static readonly VectorSearchOptions<TRecord> s_defaultVectorSearchOptions = new();

    /// <summary>Metadata about vector store record collection.</summary>
    private readonly VectorStoreCollectionMetadata _collectionMetadata;

    /// <summary>Data source used to interact with the database.</summary>
    private readonly SingleStoreDataSource _dataSource;

    private readonly SingleStoreDataSourceArc? _dataSourceArc;
    private readonly string _databaseName;

    /// <summary>A mapper to use for converting between the data model and the Azure AI Search record.</summary>
    private readonly SingleStoreMapper<TRecord> _mapper;

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
            _mapper = new SingleStoreMapper<TRecord>(_model);


            _dataSource = dataSource;
            _dataSourceArc = dataSourceArc;
            _databaseName = new SingleStoreConnectionStringBuilder(dataSource.ConnectionString).Database!;
            _collectionMetadata = new VectorStoreCollectionMetadata
            {
                VectorStoreSystemName = SingleStoreConstants.VectorStoreSystemName,
                VectorStoreName = _databaseName,
                CollectionName = name
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

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc cref="VectorStoreCollection{TKey, TRecord}.GetService(Type, object?)" />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Verify.NotNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreCollectionMetadata) ? _collectionMetadata :
            serviceType == typeof(SingleStoreDataSource) ? _dataSource :
            serviceType.IsInstanceOfType(this) ? this :
            null;
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
    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.ShowTables(connection, _databaseName, Name);

        return await connection.ExecuteWithErrorHandlingAsync(_collectionMetadata,
            "CollectionExists",
            async () =>
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.CreateTable(connection,
            _databaseName,
            Name,
            _model);

        await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "EnsureCollectionExists",
            () => command.ExecuteNonQueryAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.DropTableIfExists(connection,
            _databaseName,
            Name);

        await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "DeleteCollection",
            () => command.ExecuteNonQueryAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TRecord?> GetAsync(TKey key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var includeVectors = options?.IncludeVectors is true;
        if (includeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SingleStoreSqlBuilder.Select(connection, _databaseName, Name, _model, key, includeVectors);

        return await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "Get",
            async () =>
            {
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return reader.HasRows
                    ? _mapper.MapFromStorageToDataModel(reader, includeVectors)
                    : null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var includeVectors = options?.IncludeVectors is true;
        if (includeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        var listOfKeys = keys.ToList();
        if (listOfKeys.Count == 0)
        {
            yield break;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SingleStoreSqlBuilder.SelectBatch(connection, _databaseName, Name, _model, listOfKeys, includeVectors);

        using var reader = await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "GetBatch",
            () => command.ExecuteReaderAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadWithErrorHandlingAsync(_collectionMetadata, "GetBatch", cancellationToken).ConfigureAwait(false))
        {
            yield return _mapper.MapFromStorageToDataModel(reader, includeVectors);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(filter);
        Verify.NotLessThan(top, 1);

        options ??= new FilteredRecordRetrievalOptions<TRecord>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.SelectWhere(connection, _databaseName, Name, _model, filter, top, options);

        await using var reader = await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "GetWithFilter",
            () => command.ExecuteReaderAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadWithErrorHandlingAsync(_collectionMetadata, "GetWithFilter", cancellationToken).ConfigureAwait(false))
        {
            yield return _mapper.MapFromStorageToDataModel(reader, options.IncludeVectors);
        }
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.Delete(
            connection,
            _databaseName,
            Name,
            _model.KeyProperty,
            key);

        await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "Delete",
            () => command.ExecuteNonQueryAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);
        var listOfKeys = keys.ToList();
        if (listOfKeys.Count == 0)
        {
            return;
        }


        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SingleStoreSqlBuilder.DeleteBatch(
            connection,
            _databaseName,
            Name,
            _model.KeyProperty,
            listOfKeys);

        await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "DeleteBatch",
            () => command.ExecuteNonQueryAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        await UpsertAsync([record], cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);
        IReadOnlyList<TRecord>? recordsList = null;

        // If an embedding generator is defined, invoke it once per property for all records.
        Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>? generatedEmbeddings = null;

        var vectorPropertyCount = _model.VectorProperties.Count;
        for (var i = 0; i < vectorPropertyCount; i++)
        {
            var vectorProperty = _model.VectorProperties[i];

            if (SingleStoreModelBuilder.IsVectorPropertyTypeValidCore(vectorProperty.Type, out _))
            {
                continue;
            }

            // We have a vector property whose type isn't natively supported - we need to generate embeddings.
            Debug.Assert(vectorProperty.EmbeddingGenerator is not null);

            // Materialize the records' enumerable if needed, to prevent multiple enumeration.
            if (recordsList is null)
            {
                recordsList = records is IReadOnlyList<TRecord> r ? r : records.ToList();

                if (recordsList.Count == 0)
                {
                    return;
                }

                records = recordsList;
            }

            // TODO: Ideally we'd group together vector properties using the same generator (and with the same input and output properties),
            // and generate embeddings for them in a single batch. That's some more complexity though.
            generatedEmbeddings ??= new Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>(vectorPropertyCount);
            generatedEmbeddings[vectorProperty] = await vectorProperty.GenerateEmbeddingsAsync(records.Select(r => vectorProperty.GetValueAsObject(r)), cancellationToken).ConfigureAwait(false);
        }

        var keyProperty = _model.KeyProperty;
        if (keyProperty.IsAutoGenerated)
        {
            // Materialize the records' enumerable if needed, to prevent multiple enumeration.
            if (recordsList is null)
            {
                recordsList = records is IReadOnlyList<TRecord> r ? r : records.ToList();

                if (recordsList.Count == 0)
                {
                    return;
                }

                records = recordsList;
            }

            foreach (var record in recordsList!)
            {
                if (keyProperty.GetValue<Guid>(record) == Guid.Empty)
                {
                    keyProperty.SetValue(record, Guid.NewGuid());
                }
            }
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var pipe = new Pipe();

        var loader = new SingleStoreBulkLoader(connection)
        {
            SourceStream = pipe.Reader.AsStream(),
            TableName = SingleStoreSqlBuilder.QuoteTable(_databaseName, Name),
            Local = true,
            CharacterSet = "utf8mb4",
            FieldTerminator = "\t",
            LineTerminator = "\n",
            EscapeCharacter = '\\',
            ConflictOption = SingleStoreBulkLoaderConflictOption.Replace
        };
        loader.Columns.AddRange(_model.Properties.Select(propery => SingleStoreSqlBuilder.Builder.QuoteIdentifier(propery.StorageName)).ToList());

        await VectorStoreErrorHandler.RunOperationAsync<SingleStoreException>(
            _collectionMetadata,
            "Upsert",
            async () =>
            {
                var writeTask = TsvWriter<TRecord>.WriteRecordsAsync(
                    pipe.Writer,
                    _model,
                    records,
                    generatedEmbeddings,
                    cancellationToken);
                var loadTask = loader.LoadAsync(cancellationToken);

                await Task.WhenAll(writeTask, loadTask).ConfigureAwait(false);
            });
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(searchValue);
        Verify.NotLessThan(top, 1);

        options ??= s_defaultVectorSearchOptions;
        if (options.IncludeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        var vectorProperty = _model.GetVectorPropertyOrSingle(options);
        object vector = searchValue switch
        {
            ReadOnlyMemory<sbyte> or ReadOnlyMemory<short> or ReadOnlyMemory<int>
                or ReadOnlyMemory<long> or ReadOnlyMemory<float> or ReadOnlyMemory<double> => searchValue,

            sbyte[] or short[] or int[] or long[] or float[] or double[] => searchValue,

            Embedding<sbyte> e => e.Vector,
            Embedding<short> e => e.Vector,
            Embedding<int> e => e.Vector,
            Embedding<long> e => e.Vector,
            Embedding<float> e => e.Vector,
            Embedding<double> e => e.Vector,

            _ when vectorProperty.EmbeddingGenerationDispatcher is not null
                => await vectorProperty.GenerateEmbeddingAsync(searchValue, cancellationToken).ConfigureAwait(false) switch
                {
                    Embedding<sbyte> e => e.Vector,
                    Embedding<short> e => e.Vector,
                    Embedding<int> e => e.Vector,
                    Embedding<long> e => e.Vector,
                    Embedding<float> e => e.Vector,
                    Embedding<double> e => e.Vector,
                    _ => throw new NotSupportedException(VectorDataStrings.InvalidSearchInputAndNoEmbeddingGeneratorWasConfigured(searchValue.GetType(), SingleStoreModelBuilder.SupportedVectorTypes))
                },

            _ => vectorProperty.EmbeddingGenerator is null
                ? throw new NotSupportedException(VectorDataStrings.InvalidSearchInputAndNoEmbeddingGeneratorWasConfigured(searchValue.GetType(), SingleStoreModelBuilder.SupportedVectorTypes))
                : throw new InvalidOperationException(VectorDataStrings.IncompatibleEmbeddingGeneratorWasConfiguredForInputType(typeof(TInput), vectorProperty.EmbeddingGenerator.GetType()))
        };

        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SingleStoreSqlBuilder.SelectVectorSearch(connection,
            _databaseName,
            Name,
            _model,
            vectorProperty,
            vector,
            options.Filter,
            options.Skip,
            options.IncludeVectors,
            top,
            options.ScoreThreshold);

        using var reader = await connection.ExecuteWithErrorHandlingAsync(
            _collectionMetadata,
            "Search",
            () => command.ExecuteReaderAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadWithErrorHandlingAsync(_collectionMetadata, "Search", cancellationToken).ConfigureAwait(false))
        {
            yield return new VectorSearchResult<TRecord>(
                _mapper.MapFromStorageToDataModel(reader, options.IncludeVectors),
                reader.GetDouble(reader.GetOrdinal(SingleStoreConstants.ScoreColumnName)));
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _dataSourceArc?.Dispose();
        base.Dispose(disposing);
    }
}
