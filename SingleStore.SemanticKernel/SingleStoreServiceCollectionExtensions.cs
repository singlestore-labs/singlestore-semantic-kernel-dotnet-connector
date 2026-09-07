using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Extension methods to register SingleStore <see cref="VectorStore" /> instances on an
/// <see cref="IServiceCollection" />.
/// </summary>
public static class SingleStoreServiceCollectionExtensions
{
    private const string DynamicCodeMessage =
        "This method is incompatible with NativeAOT, consult the documentation for adding collections in a way that's compatible with NativeAOT.";

    private const string UnreferencedCodeMessage =
        "This method is incompatible with trimming, consult the documentation for adding collections in a way that's compatible with NativeAOT.";


    /// <summary>
    /// Register a <see cref="SingleStoreVectorStore" /> as <see cref="VectorStore" />, where the
    /// <see cref="SingleStoreDataSource" /> is retrieved from the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to register the <see cref="SingleStoreVectorStore" /> on.</param>
    /// <param name="options">Optional options to further configure the <see cref="VectorStore" />.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton" />.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// Upserts use LOAD DATA LOCAL INFILE. The <see cref="SingleStoreDataSource" /> registered in the container
    /// must have <c>AllowLoadLocalInfile=true</c>; otherwise <c>UpsertAsync</c> will fail.
    /// </remarks>
    public static IServiceCollection AddSingleStoreVectorStore(this IServiceCollection services,
        SingleStoreVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Verify.NotNull(services);

        services.Add(new ServiceDescriptor(typeof(SingleStoreVectorStore),
            sp =>
            {
                var dataSource = sp.GetRequiredService<SingleStoreDataSource>();
                var copy = GetStoreOptions(sp, _ => options);

                // The data source has been solved from the DI container, so we do not own it.
                return new SingleStoreVectorStore(dataSource, false, copy);
            },
            lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStore),
            static sp => sp.GetRequiredService<SingleStoreVectorStore>(),
            lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="SingleStoreVectorStore" /> as <see cref="VectorStore" />, with the specified connection
    /// string
    /// and service lifetime.
    /// </summary>
    /// <inheritdoc
    ///     cref="AddKeyedSingleStoreVectorStore(IServiceCollection, object, string, SingleStoreVectorStoreOptions?, ServiceLifetime)" />
    public static IServiceCollection AddSingleStoreVectorStore(this IServiceCollection services,
        string connectionString,
        SingleStoreVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Verify.NotNullOrWhiteSpace(connectionString);

        return AddVectorStore(services, null, sp => connectionString, sp => options, lifetime);
    }

    /// <summary>
    /// Registers a keyed <see cref="SingleStoreVectorStore" /> as <see cref="VectorStore" />, with the specified
    /// connection
    /// string and service lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to register the <see cref="SingleStoreVectorStore" /> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="connectionString">SingleStore database connection string.</param>
    /// <param name="options">Optional options to further configure the <see cref="SingleStoreVectorStore" />.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton" />.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeyedSingleStoreVectorStore(this IServiceCollection services,
        object? serviceKey,
        string connectionString,
        SingleStoreVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Verify.NotNullOrWhiteSpace(connectionString);

        return AddVectorStore(services, serviceKey, sp => connectionString, sp => options, lifetime);
    }

    /// <summary>
    /// Registers a <see cref="SingleStoreVectorStore" /> as <see cref="VectorStore" />, with the specified connection
    /// string
    /// and service lifetime.
    /// </summary>
    /// <inheritdoc
    ///     cref="AddVectorStore(IServiceCollection, object?, Func{IServiceProvider, string}, Func{IServiceProvider, SingleStoreVectorStoreOptions?}?, ServiceLifetime)" />
    public static IServiceCollection AddSingleStoreVectorStore(this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, SingleStoreVectorStoreOptions?>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return AddVectorStore(services, null, connectionStringProvider, optionsProvider, lifetime);
    }

    /// <inheritdoc
    ///     cref="AddVectorStore(IServiceCollection, object?, Func{IServiceProvider, string}, Func{IServiceProvider, SingleStoreVectorStoreOptions?}?, ServiceLifetime)" />
    public static IServiceCollection AddKeyedSingleStoreVectorStore(this IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, SingleStoreVectorStoreOptions?>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return AddVectorStore(services, serviceKey, connectionStringProvider, optionsProvider, lifetime);
    }

    /// <summary>
    /// Registers a keyed <see cref="SingleStoreVectorStore" /> as <see cref="VectorStore" />, with the specified
    /// connection
    /// string and service lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to register the <see cref="SingleStoreVectorStore" /> on.</param>
    /// <param name="serviceKey">The key with which to associate the store.</param>
    /// <param name="connectionStringProvider">The connection string provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the <see cref="SingleStoreVectorStore" />.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton" />.</param>
    /// <returns>The service collection.</returns>
    private static IServiceCollection AddVectorStore(IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, SingleStoreVectorStoreOptions?>? optionsProvider,
        ServiceLifetime lifetime)
    {
        Verify.NotNull(services);
        Verify.NotNull(connectionStringProvider);

        services.Add(new ServiceDescriptor(typeof(SingleStoreVectorStore),
            serviceKey,
            (sp, _) =>
            {
                var connectionString = connectionStringProvider(sp);
                var options = GetStoreOptions(sp, optionsProvider);

                return new SingleStoreVectorStore(connectionString, options);
            },
            lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStore),
            serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<SingleStoreVectorStore>(key),
            lifetime));

        return services;
    }

    /// <summary>
    /// Register a <see cref="SingleStoreCollection{TKey, TRecord}" /> where the <see cref="SingleStoreDataSource" /> is
    /// retrieved from the dependency injection container.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="services">
    /// The <see cref="IServiceCollection" /> to register the
    /// <see cref="VectorStoreCollection{TKey, TRecord}" /> on.
    /// </param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Optional options to further configure the <see cref="VectorStoreCollection{TKey, TRecord}" />.</param>
    /// <param name="lifetime">
    /// The service lifetime for the store. It needs to match <see cref="SingleStoreDataSource" /> lifetime.
    /// Defaults to <see cref="ServiceLifetime.Singleton" />.
    /// </param>
    /// <returns>Service collection.</returns>
    /// <remarks>
    /// Upserts use LOAD DATA LOCAL INFILE. The <see cref="SingleStoreDataSource" /> registered in the container
    /// must have <c>AllowLoadLocalInfile=true</c>; otherwise <c>UpsertAsync</c> will fail.
    /// </remarks>
    [RequiresDynamicCode(DynamicCodeMessage)]
    [RequiresUnreferencedCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddSingleStoreCollection<TKey, TRecord>(this IServiceCollection services,
        string name,
        SingleStoreCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TKey : notnull where TRecord : class
    {
        Verify.NotNull(services);
        Verify.NotNullOrWhiteSpace(name);

        services.Add(new ServiceDescriptor(typeof(SingleStoreCollection<TKey, TRecord>),
            sp =>
            {
                var dataSource = sp.GetRequiredService<SingleStoreDataSource>();
                var copy = GetCollectionOptions(sp, _ => options);

                // The data source has been solved from the DI container, so we do not own it.
                return new SingleStoreCollection<TKey, TRecord>(dataSource, name, false, copy);
            },
            lifetime));

        AddAbstractions<TKey, TRecord>(services, null, lifetime);

        return services;
    }

    /// <summary>
    /// Registers a <see cref="SingleStoreCollection{TKey, TRecord}" /> as
    /// <see cref="VectorStoreCollection{TKey, TRecord}" />, with the specified connection string and service lifetime.
    /// </summary>
    /// <inheritdoc
    ///     cref="AddKeyedSingleStoreCollection{TKey, TRecord}(IServiceCollection, object, string, string, SingleStoreCollectionOptions?, ServiceLifetime)" />
    [RequiresDynamicCode(DynamicCodeMessage)]
    [RequiresUnreferencedCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddSingleStoreCollection<TKey, TRecord>(this IServiceCollection services,
        string name,
        string connectionString,
        SingleStoreCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TKey : notnull where TRecord : class
    {
        Verify.NotNullOrWhiteSpace(connectionString);

        return services.AddKeyedSingleStoreCollection<TKey, TRecord>(null,
            name,
            sp => connectionString,
            sp => options,
            lifetime);
    }

    /// <summary>
    /// Registers a keyed <see cref="SingleStoreCollection{TKey, TRecord}" /> as
    /// <see cref="VectorStoreCollection{TKey, TRecord}" />, with the specified connection string and service lifetime.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="services">
    /// The <see cref="IServiceCollection" /> to register the
    /// <see cref="VectorStoreCollection{TKey, TRecord}" /> on.
    /// </param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="connectionString">SingleStore database connection string.</param>
    /// <param name="options">Optional options to further configure the <see cref="VectorStoreCollection{TKey, TRecord}" />.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton" />.</param>
    /// <returns>Service collection.</returns>
    [RequiresDynamicCode(DynamicCodeMessage)]
    [RequiresUnreferencedCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedSingleStoreCollection<TKey, TRecord>(this IServiceCollection services,
        object? serviceKey,
        string name,
        string connectionString,
        SingleStoreCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TKey : notnull where TRecord : class
    {
        Verify.NotNullOrWhiteSpace(connectionString);

        return services.AddKeyedSingleStoreCollection<TKey, TRecord>(serviceKey,
            name,
            sp => connectionString,
            sp => options,
            lifetime);
    }

    /// <summary>
    /// Registers a <see cref="SingleStoreCollection{TKey, TRecord}" /> as
    /// <see cref="VectorStoreCollection{TKey, TRecord}" />
    /// , with the specified connection string and service lifetime.
    /// </summary>
    /// <inheritdoc
    ///     cref="AddKeyedSingleStoreCollection{TKey, TRecord}(IServiceCollection, object?, string, Func{IServiceProvider, string}, Func{IServiceProvider, SingleStoreCollectionOptions?}?, ServiceLifetime)" />
    [RequiresDynamicCode(DynamicCodeMessage)]
    [RequiresUnreferencedCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddSingleStoreCollection<TKey, TRecord>(this IServiceCollection services,
        string name,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, SingleStoreCollectionOptions?>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TKey : notnull where TRecord : class
    {
        return services.AddKeyedSingleStoreCollection<TKey, TRecord>(null,
            name,
            connectionStringProvider,
            optionsProvider,
            lifetime);
    }

    /// <summary>
    /// Registers a keyed <see cref="SingleStoreCollection{TKey, TRecord}" /> as
    /// <see cref="VectorStoreCollection{TKey, TRecord}" />, with the specified connection string and service lifetime.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection" /> to register the
    /// <see cref="VectorStoreCollection{TKey, TRecord}" /> on.
    /// </param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="connectionStringProvider">The connection string provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the collection.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton" />.</param>
    /// <returns>The service collection.</returns>
    [RequiresDynamicCode(DynamicCodeMessage)]
    [RequiresUnreferencedCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedSingleStoreCollection<TKey, TRecord>(this IServiceCollection services,
        object? serviceKey,
        string name,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, SingleStoreCollectionOptions?>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TKey : notnull where TRecord : class
    {
        Verify.NotNull(services);
        Verify.NotNullOrWhiteSpace(name);
        Verify.NotNull(connectionStringProvider);

        services.Add(new ServiceDescriptor(typeof(SingleStoreCollection<TKey, TRecord>),
            serviceKey,
            (sp, _) =>
            {
                var connectionString = connectionStringProvider(sp);
                var options = GetCollectionOptions(sp, optionsProvider);
                return new SingleStoreCollection<TKey, TRecord>(connectionString, name, options);
            },
            lifetime));

        AddAbstractions<TKey, TRecord>(services, serviceKey, lifetime);

        return services;
    }

    private static void AddAbstractions<TKey, TRecord>(IServiceCollection services,
        object? serviceKey,
        ServiceLifetime lifetime) where TKey : notnull where TRecord : class
    {
        services.Add(new ServiceDescriptor(typeof(VectorStoreCollection<TKey, TRecord>),
            serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<SingleStoreCollection<TKey, TRecord>>(key),
            lifetime));

        services.Add(new ServiceDescriptor(typeof(IVectorSearchable<TRecord>),
            serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<SingleStoreCollection<TKey, TRecord>>(key),
            lifetime));

        services.Add(new ServiceDescriptor(typeof(IKeywordHybridSearchable<TRecord>),
            serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<SingleStoreCollection<TKey, TRecord>>(key),
            lifetime));
    }

    private static SingleStoreVectorStoreOptions? GetStoreOptions(IServiceProvider sp,
        Func<IServiceProvider, SingleStoreVectorStoreOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
            return options; // The user has provided everything, there is nothing to change.

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new SingleStoreVectorStoreOptions(options)
            {
                EmbeddingGenerator = embeddingGenerator
            }; // Create a brand new copy in order to avoid modifying the original options.
    }

    private static SingleStoreCollectionOptions? GetCollectionOptions(IServiceProvider sp,
        Func<IServiceProvider, SingleStoreCollectionOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
            return options; // The user has provided everything, there is nothing to change.

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new SingleStoreCollectionOptions(options)
            {
                EmbeddingGenerator = embeddingGenerator
            }; // Create a brand new copy in order to avoid modifying the original options.
    }
}
