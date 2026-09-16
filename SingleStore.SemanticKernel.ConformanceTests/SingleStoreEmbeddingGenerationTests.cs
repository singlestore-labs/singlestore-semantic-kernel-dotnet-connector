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
                .AddSingleStoreCollection<int, RecordWithAttributes>(CollectionName,
                    new SingleStoreCollectionOptions { Definition = CreateAttributeRecordDefinition() })
        ];

        /// <summary>
        /// Mirrors the attributes on <see cref="RecordWithAttributes" />, but pins the distance function.
        /// </summary>
        /// <remarks>
        /// <see cref="RecordWithAttributes" /> leaves <c>DistanceFunction</c> unset, which SingleStore resolves to
        /// dot product; the shared test data only ranks as the tests expect under the store's default distance
        /// function. No embedding generator is set here so that the one registered in the container still applies.
        /// </remarks>
        private VectorStoreCollectionDefinition CreateAttributeRecordDefinition()
        {
            return new VectorStoreCollectionDefinition
            {
                Properties =
                [
                    new VectorStoreKeyProperty(nameof(RecordWithAttributes.Key), typeof(int)),
                    new VectorStoreVectorProperty(nameof(RecordWithAttributes.Embedding),
                        typeof(string),
                        3)
                    {
                        DistanceFunction = DefaultDistanceFunction,
                        IndexKind = DefaultIndexKind
                    },
                    new VectorStoreDataProperty(nameof(RecordWithAttributes.Counter), typeof(int))
                        { IsIndexed = true },
                    new VectorStoreDataProperty(nameof(RecordWithAttributes.Text), typeof(string))
                ]
            };
        }
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
