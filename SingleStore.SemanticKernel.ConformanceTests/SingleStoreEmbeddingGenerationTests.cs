using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreEmbeddingGenerationTests(
    SingleStoreEmbeddingGenerationTests.StringVectorFixture stringVectorFixture,
    SingleStoreEmbeddingGenerationTests.RomOfFloatVectorFixture romOfFloatVectorFixture)
    : EmbeddingGenerationTests<int>(stringVectorFixture, romOfFloatVectorFixture),
        IClassFixture<SingleStoreEmbeddingGenerationTests.StringVectorFixture>,
        IClassFixture<SingleStoreEmbeddingGenerationTests.RomOfFloatVectorFixture>
{
    public new class StringVectorFixture : EmbeddingGenerationTests<int>.StringVectorFixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services.AddSingleton(SingleStoreTestStore.Instance.DataSource).AddSingleStoreVectorStore()
        ];

        public override Func<IServiceCollection, IServiceCollection>[]
            DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services.AddSingleton(SingleStoreTestStore.Instance.DataSource)
                .AddSingleStoreCollection<int, RecordWithAttributes>(CollectionName)
        ];

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
        {
            return SingleStoreTestStore.Instance.GetVectorStore(
                new SingleStoreVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });
        }
    }

    public new class RomOfFloatVectorFixture : EmbeddingGenerationTests<int>.RomOfFloatVectorFixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services.AddSingleton(SingleStoreTestStore.Instance.DataSource).AddSingleStoreVectorStore()
        ];

        public override Func<IServiceCollection, IServiceCollection>[]
            DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services.AddSingleton(SingleStoreTestStore.Instance.DataSource)
                .AddSingleStoreCollection<int, RecordWithAttributes>(CollectionName)
        ];

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
        {
            return SingleStoreTestStore.Instance.GetVectorStore(
                new SingleStoreVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });
        }
    }
}
