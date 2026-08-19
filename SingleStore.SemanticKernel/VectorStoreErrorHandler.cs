using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Contains helpers for reading vector store model properties and their attributes.
/// Copied from: https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/InternalUtilities/src/Data/VectorStoreErrorHandler.cs
/// </summary>
[ExcludeFromCodeCoverage]
internal static class VectorStoreErrorHandler
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> RunOperationAsync<TResult, TException>(
        VectorStoreMetadata metadata,
        string operationName,
        Func<Task<TResult>> operation)
        where TException : Exception
    {
        return RunOperationAsync<TResult, TException>(
            new VectorStoreCollectionMetadata
            {
                CollectionName = null,
                VectorStoreName = metadata.VectorStoreName,
                VectorStoreSystemName = metadata.VectorStoreSystemName
            },
            operationName,
            operation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<TResult> RunOperationAsync<TResult, TException>(
        VectorStoreCollectionMetadata metadata,
        string operationName,
        Func<Task<TResult>> operation)
        where TException : Exception
    {
        try
        {
            return await operation.Invoke().ConfigureAwait(false);
        }
        catch (AggregateException ex) when (ex.InnerException is TException innerEx)
        {
            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
        catch (TException ex)
        {
            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult RunOperation<TResult, TException>(
        VectorStoreMetadata metadata,
        string operationName,
        Func<TResult> operation)
        where TException : Exception
    {
        return RunOperation<TResult, TException>(
            new VectorStoreCollectionMetadata
            {
                CollectionName = null,
                VectorStoreName = metadata.VectorStoreName,
                VectorStoreSystemName = metadata.VectorStoreSystemName
            },
            operationName,
            operation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult RunOperation<TResult, TException>(
        VectorStoreCollectionMetadata metadata,
        string operationName,
        Func<TResult> operation)
        where TException : Exception
    {
        try
        {
            return operation.Invoke();
        }
        catch (AggregateException ex) when (ex.InnerException is TException innerEx)
        {
            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
        catch (TException ex)
        {
            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<TResult> RunOperationWithRetryAsync<TResult, TException>(
        VectorStoreCollectionMetadata metadata,
        string operationName,
        int maxRetries,
        int delayInMilliseconds,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
        where TException : Exception
    {
        var retries = 0;

        var exceptions = new List<Exception>();

        while (retries < maxRetries)
        {
            try
            {
                return await operation.Invoke().ConfigureAwait(false);
            }
            catch (AggregateException ex) when (ex.InnerException is TException innerEx)
            {
                retries++;
                exceptions.Add(ex);

                if (retries >= maxRetries)
                {
                    throw new VectorStoreException("Call to vector store failed.", new AggregateException(exceptions))
                    {
                        VectorStoreSystemName = metadata.VectorStoreSystemName,
                        VectorStoreName = metadata.VectorStoreName,
                        CollectionName = metadata.CollectionName,
                        OperationName = operationName
                    };
                }

                await Task.Delay(delayInMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (TException ex)
            {
                retries++;
                exceptions.Add(ex);

                if (retries >= maxRetries)
                {
                    throw new VectorStoreException("Call to vector store failed.", new AggregateException(exceptions))
                    {
                        VectorStoreSystemName = metadata.VectorStoreSystemName,
                        VectorStoreName = metadata.VectorStoreName,
                        CollectionName = metadata.CollectionName,
                        OperationName = operationName
                    };
                }

                await Task.Delay(delayInMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new VectorStoreException("Call to vector store failed.", new AggregateException(exceptions))
        {
            VectorStoreSystemName = metadata.VectorStoreSystemName,
            VectorStoreName = metadata.VectorStoreName,
            CollectionName = metadata.CollectionName,
            OperationName = operationName
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task RunOperationAsync<TException>(
        VectorStoreCollectionMetadata metadata,
        string operationName,
        Func<Task> operation)
        where TException : Exception
    {
        try
        {
            await operation.Invoke().ConfigureAwait(false);
        }
        catch (AggregateException ex) when (ex.InnerException is TException innerEx)
        {
            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
        catch (TException ex)
        {
            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task RunOperationWithRetryAsync<TException>(
        VectorStoreCollectionMetadata metadata,
        string operationName,
        int maxRetries,
        int delayInMilliseconds,
        Func<Task> operation,
        CancellationToken cancellationToken)
        where TException : Exception
    {
        var retries = 0;

        var exceptions = new List<Exception>();

        while (retries < maxRetries)
        {
            try
            {
                await operation.Invoke().ConfigureAwait(false);
                return;
            }
            catch (AggregateException ex) when (ex.InnerException is TException innerEx)
            {
                retries++;
                exceptions.Add(ex);

                if (retries >= maxRetries)
                {
                    throw new VectorStoreException("Call to vector store failed.", new AggregateException(exceptions))
                    {
                        VectorStoreSystemName = metadata.VectorStoreSystemName,
                        VectorStoreName = metadata.VectorStoreName,
                        CollectionName = metadata.CollectionName,
                        OperationName = operationName
                    };
                }

                await Task.Delay(delayInMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (TException ex)
            {
                retries++;
                exceptions.Add(ex);

                if (retries >= maxRetries)
                {
                    throw new VectorStoreException("Call to vector store failed.", new AggregateException(exceptions))
                    {
                        VectorStoreSystemName = metadata.VectorStoreSystemName,
                        VectorStoreName = metadata.VectorStoreName,
                        CollectionName = metadata.CollectionName,
                        OperationName = operationName
                    };
                }

                await Task.Delay(delayInMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new VectorStoreException("Call to vector store failed.", new AggregateException(exceptions))
        {
            VectorStoreSystemName = metadata.VectorStoreSystemName,
            VectorStoreName = metadata.VectorStoreName,
            CollectionName = metadata.CollectionName,
            OperationName = operationName
        };
    }

    internal static Task<bool> ReadWithErrorHandlingAsync(
        this DbDataReader reader,
        VectorStoreCollectionMetadata metadata,
        string operationName,
        CancellationToken cancellationToken)
    {
        return RunOperationAsync<bool, DbException>(
            metadata,
            operationName,
            () => reader.ReadAsync(cancellationToken));
    }

    internal static Task<bool> ReadWithErrorHandlingAsync(
        this DbDataReader reader,
        VectorStoreMetadata metadata,
        string operationName,
        CancellationToken cancellationToken)
    {
        return RunOperationAsync<bool, DbException>(
            metadata,
            operationName,
            () => reader.ReadAsync(cancellationToken));
    }

    internal static async Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(
        this DbConnection connection,
        VectorStoreMetadata metadata,
        string operationName,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteWithErrorHandlingAsync(new VectorStoreCollectionMetadata
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = null
            },
            operationName,
            operation,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(
        this DbConnection connection,
        VectorStoreCollectionMetadata metadata,
        string operationName,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (DbException ex)
        {
#if NET
            await connection.DisposeAsync().ConfigureAwait(false);
#else
            connection.Dispose();
#endif

            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
        catch (IOException ex)
        {
#if NET
            await connection.DisposeAsync().ConfigureAwait(false);
#else
            connection.Dispose();
#endif

            throw new VectorStoreException("Call to vector store failed.", ex)
            {
                VectorStoreSystemName = metadata.VectorStoreSystemName,
                VectorStoreName = metadata.VectorStoreName,
                CollectionName = metadata.CollectionName,
                OperationName = operationName
            };
        }
        catch (Exception)
        {
#if NET
            await connection.DisposeAsync().ConfigureAwait(false);
#else
            connection.Dispose();
#endif
            throw;
        }
    }

    public struct ConfiguredCancelableErrorHandlingAsyncEnumerable<TResult, TException>
        where TException : Exception
    {
        private readonly ConfiguredCancelableAsyncEnumerable<TResult> _enumerable;
        private readonly VectorStoreCollectionMetadata _metadata;
        private readonly string _operationName;

        public ConfiguredCancelableErrorHandlingAsyncEnumerable(
            ConfiguredCancelableAsyncEnumerable<TResult> enumerable,
            VectorStoreCollectionMetadata metadata,
            string operationName)
        {
            _enumerable = enumerable;
            _metadata = metadata;
            _operationName = operationName;
        }

        public ConfiguredCancelableErrorHandlingAsyncEnumerable(
            ConfiguredCancelableAsyncEnumerable<TResult> enumerable,
            VectorStoreMetadata metadata,
            string operationName)
        {
            _enumerable = enumerable;
            _metadata = new VectorStoreCollectionMetadata
            {
                CollectionName = null,
                VectorStoreName = metadata.VectorStoreName,
                VectorStoreSystemName = metadata.VectorStoreSystemName
            };
            _operationName = operationName;
        }

        public Enumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new Enumerator(_enumerable.WithCancellation(cancellationToken).GetAsyncEnumerator(), _metadata, _operationName);
        }

        public ConfiguredCancelableErrorHandlingAsyncEnumerable<TResult, TException> ConfigureAwait(bool continueOnCapturedContext)
        {
            return new ConfiguredCancelableErrorHandlingAsyncEnumerable<TResult, TException>(_enumerable.ConfigureAwait(continueOnCapturedContext), _metadata, _operationName);
        }

        public struct Enumerator(
            ConfiguredCancelableAsyncEnumerable<TResult>.Enumerator enumerator,
            VectorStoreCollectionMetadata metadata,
            string operationName)
        {
            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    return await enumerator.MoveNextAsync();
                }
                catch (AggregateException ex) when (ex.InnerException is TException innerEx)
                {
                    throw new VectorStoreException("Call to vector store failed.", ex)
                    {
                        VectorStoreSystemName = metadata.VectorStoreSystemName,
                        VectorStoreName = metadata.VectorStoreName,
                        CollectionName = metadata.CollectionName,
                        OperationName = operationName
                    };
                }
                catch (TException ex)
                {
                    throw new VectorStoreException("Call to vector store failed.", ex)
                    {
                        VectorStoreSystemName = metadata.VectorStoreSystemName,
                        VectorStoreName = metadata.VectorStoreName,
                        CollectionName = metadata.CollectionName,
                        OperationName = operationName
                    };
                }
            }

            public TResult Current => enumerator.Current;
        }
    }
}
