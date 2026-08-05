using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VectorData.ConformanceTests;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreDependencyInjectionTests : DependencyInjectionTests<SingleStoreVectorStore,
    SingleStoreCollection<string, DependencyInjectionTests<string>.Record>, string,
    DependencyInjectionTests<string>.Record>
{
    protected const string ConnectionString = "Host=localhost;Database=test;";

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>>
        CollectionDelegates
    {
        get
        {
            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddSingleStoreCollection<string, Record>(name, ConnectionString, lifetime: lifetime)
                : services.AddKeyedSingleStoreCollection<string, Record>(serviceKey,
                    name,
                    ConnectionString,
                    lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddSingleStoreCollection<string, Record>(name, ConnectionStringProvider, lifetime: lifetime)
                : services.AddKeyedSingleStoreCollection<string, Record>(serviceKey,
                    name,
                    sp => ConnectionStringProvider(sp, serviceKey),
                    lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddSingleStoreVectorStore(ConnectionString, lifetime: lifetime)
                : services.AddKeyedSingleStoreVectorStore(serviceKey, ConnectionString, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddSingleStoreVectorStore(ConnectionStringProvider, lifetime: lifetime)
                : services.AddKeyedSingleStoreVectorStore(serviceKey,
                    sp => ConnectionStringProvider(sp, serviceKey),
                    lifetime: lifetime);
        }
    }

    protected override void PopulateConfiguration(ConfigurationManager configuration, object? serviceKey = null)
    {
        configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(CreateConfigKey("SingleStore", serviceKey, "ConnectionString"),
                ConnectionString)
        ]);
    }

    private static string ConnectionStringProvider(IServiceProvider sp)
    {
        return sp.GetRequiredService<IConfiguration>().GetRequiredSection("SingleStore:ConnectionString").Value!;
    }

    private static string ConnectionStringProvider(IServiceProvider sp, object serviceKey)
    {
        return sp.GetRequiredService<IConfiguration>()
            .GetRequiredSection(CreateConfigKey("SingleStore", serviceKey, "ConnectionString")).Value!;
    }

    [Fact]
    public void ConnectionStringProviderCantBeNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddSingleStoreCollection<string, Record>("notNull",
                connectionStringProvider: null!));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddKeyedSingleStoreCollection<string, Record>("notNull",
                "notNull",
                connectionStringProvider: null!));
    }

    [Fact]
    public void ConnectionStringCantBeNullOrEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddSingleStoreVectorStore(connectionString: null!));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddKeyedSingleStoreVectorStore("notNull", connectionString: null!));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddSingleStoreCollection<string, Record>("notNull", connectionString: null!));
        Assert.Throws<ArgumentException>(() =>
            services.AddSingleStoreCollection<string, Record>("notNull", ""));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddKeyedSingleStoreCollection<string, Record>("notNull", "notNull", connectionString: null!));
        Assert.Throws<ArgumentException>(() =>
            services.AddKeyedSingleStoreCollection<string, Record>("notNull", "notNull", ""));
    }
}
