using Microsoft.Extensions.AI;

namespace SingleStore.SemanticKernel;

/// <summary>
///     Options when creating a <see cref="SingleStoreVectorStore" />.
/// </summary>
public sealed class SingleStoreVectorStoreOptions
{
    internal static readonly SingleStoreVectorStoreOptions Default = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="SingleStoreVectorStoreOptions" /> class.
    /// </summary>
    public SingleStoreVectorStoreOptions()
    {
    }

    internal SingleStoreVectorStoreOptions(SingleStoreVectorStoreOptions? source)
    {
        EmbeddingGenerator = source?.EmbeddingGenerator;
    }

    /// <summary>
    ///     Gets or sets the default embedding generator to use when generating vectors embeddings with this vector store.
    /// </summary>
    public IEmbeddingGenerator? EmbeddingGenerator { get; set; }
}
