using Microsoft.Extensions.VectorData;
using System.Diagnostics.CodeAnalysis;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Represents a vector store implementation using SingleStore.
/// </summary>
public class SingleStoreVectorStore : VectorStore
{
    /// <inheritdoc />
    [RequiresDynamicCode(
        "This API is not compatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, use GetDynamicCollection() instead.")]
    [RequiresUnreferencedCode(
        "This API is not compatible with trimming. For dynamic mapping via Dictionary<string, object?>, use GetDynamicCollection() instead.")]
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<string> ListCollectionNamesAsync(
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(
        string name,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }
}