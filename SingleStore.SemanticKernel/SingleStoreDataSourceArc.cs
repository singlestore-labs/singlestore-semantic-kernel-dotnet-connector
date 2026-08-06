using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// A reference-counting wrapper around an <see cref="SingleStoreDataSource" /> instance.
/// </summary>
internal sealed class SingleStoreDataSourceArc(SingleStoreDataSource dataSource) : IDisposable
{
    private int _referenceCount;

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _referenceCount) == 0) dataSource.Dispose();
    }

    internal SingleStoreDataSourceArc IncrementReferenceCount()
    {
        Interlocked.Increment(ref _referenceCount);

        return this;
    }
}
