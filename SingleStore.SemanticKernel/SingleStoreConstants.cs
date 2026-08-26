namespace SingleStore.SemanticKernel;

internal class SingleStoreConstants
{
    /// <summary>The name of this vector store for telemetry purposes.</summary>
    public const string VectorStoreSystemName = "singlestore";

    /// <summary>The name of the column that returns distance value in the database.</summary>
    /// <remarks>It is used in the similarity search query. Must not conflict with model property.</remarks>
    public const string DistanceColumnName = "sk_s2_distance";
}
