using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Represents a collection of vector store records in a SingleStore database, mapped to a dynamic
/// <c>Dictionary&lt;string, object?&gt;</c>.
/// </summary>
public sealed class SingleStoreDynamicCollection : SingleStoreCollection<object, Dictionary<string, object?>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreDynamicCollection" /> class.
    /// </summary>
    /// <param name="dataSource">
    /// The data source to use for connecting to the database. Upserts require <c>AllowLoadLocalInfile=true</c> on this data source.
    /// </param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="ownsDataSource">
    /// A value indicating whether the data source should be disposed when the collection is
    /// disposed. Ownership transfers immediately, so <paramref name="dataSource" /> is also
    /// disposed if this constructor throws.
    /// </param>
    /// <param name="options">Optional configuration options for this class.</param>
    public SingleStoreDynamicCollection(SingleStoreDataSource dataSource,
        string name,
        bool ownsDataSource,
        SingleStoreCollectionOptions options)
        : this(dataSource,
            ownsDataSource ? new SingleStoreDataSourceArc(dataSource) : null,
            ownsDataSource,
            name,
            options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreCollection{TKey, TRecord}" /> class.
    /// </summary>
    /// <param name="connectionString">SingleStore database connection string.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public SingleStoreDynamicCollection(string connectionString, string name, SingleStoreCollectionOptions options)
        : this(SingleStoreUtil.CreateDataSource(connectionString), name, true, options)
    {
    }

    internal SingleStoreDynamicCollection(SingleStoreDataSource dataSource,
        SingleStoreDataSourceArc? dataSourceArc,
        bool ownsDataSource,
        string name,
        SingleStoreCollectionOptions options)
        : base(
            dataSource,
            dataSourceArc,
            ownsDataSource,
            name,
            static options => new SingleStoreModelBuilder().BuildDynamic(
                options.Definition ?? throw new ArgumentException("Definition is required for dynamic collections"),
                options.EmbeddingGenerator),
            options)
    {
    }
}
