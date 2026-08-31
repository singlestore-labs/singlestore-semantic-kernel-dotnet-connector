using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace SingleStore.SemanticKernel;

internal class SingleStoreModelBuilder() : CollectionModelBuilder(ModelBuildingOptions)
{
    public static readonly CollectionModelBuildingOptions ModelBuildingOptions = new()
    {
        RequiresAtLeastOneVector = false,
        SupportsMultipleVectors = true
    };

    /// <inheritdoc />
    protected override IReadOnlyList<EmbeddingGenerationDispatcher> EmbeddingGenerationDispatchers { get; } =
    [
        EmbeddingGenerationDispatcher.Create<Embedding<float>>(),
        EmbeddingGenerationDispatcher.Create<Embedding<double>>(),
        EmbeddingGenerationDispatcher.Create<Embedding<sbyte>>(),
        EmbeddingGenerationDispatcher.Create<Embedding<short>>(),
        EmbeddingGenerationDispatcher.Create<Embedding<int>>(),
        EmbeddingGenerationDispatcher.Create<Embedding<long>>()
    ];

    /// <inheritdoc />
    protected override bool SupportsKeyAutoGeneration(Type keyPropertyType)
    {
        return keyPropertyType == typeof(Guid);
    }

    /// <inheritdoc />
    protected override void ValidateKeyProperty(KeyPropertyModel keyProperty)
    {
        base.ValidateKeyProperty(keyProperty);

        var type = keyProperty.Type;

        if (type != typeof(short) // SMALLINT
            && type != typeof(int) // INT
            && type != typeof(long) // BIGINT
            && type != typeof(string) // LONGTEXT
            && type != typeof(Guid)) // CHAR(36)
        {
            throw new NotSupportedException(
                $"Property '{keyProperty.ModelName}' has unsupported type '{type.Name}'. Key properties must be one of the supported types: short, int, long, string, Guid");
        }
    }

    /// <inheritdoc />
    protected override bool IsDataPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes =
            "bool, byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, string, byte[], DateTime, DateTimeOffset, DateOnly, TimeOnly, Guid, string[], List<string>";

        if (Nullable.GetUnderlyingType(type) is Type underlyingType)
        {
            type = underlyingType;
        }

        return type == typeof(bool) || // TINYINT
               type == typeof(byte) || // TINYINT UNSIGNED
               type == typeof(sbyte) || // TINYINT
               type == typeof(short) || // SMALLINT
               type == typeof(ushort) || // SMALLINT UNSIGNED
               type == typeof(int) || // INT
               type == typeof(uint) || // INT UNSIGNED
               type == typeof(long) || // BIGINT
               type == typeof(ulong) || // BIGINT UNSIGNED
               type == typeof(float) || // FLOAT
               type == typeof(double) || // DOUBLE
               type == typeof(decimal) || // DECIMAL
               type == typeof(string) || // LONGTEXT
               type == typeof(byte[]) || // LONGBLOB
               type == typeof(DateTime) || // DATETIME(6)
               type == typeof(DateTimeOffset) || // DATETIME(6)
#if NET
               type == typeof(DateOnly) || // DATE
               type == typeof(TimeOnly) || // TIME(6)
#endif
               type == typeof(Guid) || // CHAR(36)
               type == typeof(string[]) || // JSON
               type == typeof(List<string>); // JSON
    }

    /// <inheritdoc />
    protected override bool IsVectorPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        return IsVectorPropertyTypeValidCore(type, out supportedTypes);
    }

    internal static bool IsVectorPropertyTypeValidCore(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes = "ReadOnlyMemory<sbyte>, Embedding<sbyte>, sbyte[], " +
                         "ReadOnlyMemory<short>, Embedding<short>, short[], " +
                         "ReadOnlyMemory<int>, Embedding<int>, int[], " +
                         "ReadOnlyMemory<long>, Embedding<long>, long[], " +
                         "ReadOnlyMemory<float>, Embedding<float>, float[], " +
                         "ReadOnlyMemory<double>, Embedding<double>, double[]";

        if (Nullable.GetUnderlyingType(type) is Type underlyingType) type = underlyingType;

        return type == typeof(ReadOnlyMemory<sbyte>)
               || type == typeof(Embedding<sbyte>)
               || type == typeof(sbyte[])
               || type == typeof(ReadOnlyMemory<short>)
               || type == typeof(Embedding<short>)
               || type == typeof(short[])
               || type == typeof(ReadOnlyMemory<int>)
               || type == typeof(Embedding<int>)
               || type == typeof(int[])
               || type == typeof(ReadOnlyMemory<long>)
               || type == typeof(Embedding<long>)
               || type == typeof(long[])
               || type == typeof(ReadOnlyMemory<float>)
               || type == typeof(Embedding<float>)
               || type == typeof(float[])
               || type == typeof(ReadOnlyMemory<double>)
               || type == typeof(Embedding<double>)
               || type == typeof(double[]);
    }
}
