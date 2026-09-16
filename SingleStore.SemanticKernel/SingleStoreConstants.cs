namespace SingleStore.SemanticKernel;

internal class SingleStoreConstants
{
    /// <summary>The name of this vector store for telemetry purposes.</summary>
    public const string VectorStoreSystemName = "singlestore";

    /// <summary>The name of the column that returns distance value.</summary>
    /// <remarks>It is used in the similarity search query. Must not conflict with model property.</remarks>
    public const string DistanceColumnName = "sk_s2_distance";

    /// <summary>The name of the column that returns final score value.</summary>
    /// <remarks>It is used in the similarity search query. Must not conflict with model property.</remarks>
    public const string ScoreColumnName = "sk_s2_score";

    /// <summary>The name of the semantic search subquery alias.</summary>
    /// <remarks>It is used in the hybrid search query. Must not conflict with collection name.</remarks>
    public const string SemanticSearchSubquery = "sk_s2_semantik_search";

    /// <summary>The name of the keyword search subquery alias.</summary>
    /// <remarks>It is used in the hybrid search query. Must not conflict with collection name.</remarks>
    public const string KeywordSearchSubquery = "sk_s2_keyword_search";

    /// <summary>The name of the column that returns rank.</summary>
    /// <remarks>It is used in the hybrid search query. Must not conflict with model property.</remarks>
    public const string HybridSearchRank = "sk_s2_hybrid_search_rank";

    /// <summary>The name of the column that returns row key.</summary>
    /// <remarks>It is used in the hybrid search query. Must not conflict with model property.</remarks>
    public const string HybridSearchId = "sk_s2_hybrid_search_id";
}
