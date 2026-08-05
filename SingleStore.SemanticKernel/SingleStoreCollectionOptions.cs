using Microsoft.Extensions.VectorData;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Options when creating a <see cref="SingleStoreCollection{TKey, TRecord}" />.
/// </summary>
public sealed class SingleStoreCollectionOptions : VectorStoreCollectionOptions
{
    internal static readonly SingleStoreCollectionOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleStoreCollectionOptions" /> class.
    /// </summary>
    public SingleStoreCollectionOptions()
    {
    }

    internal SingleStoreCollectionOptions(SingleStoreCollectionOptions? source) : base(source)
    {
    }
}
