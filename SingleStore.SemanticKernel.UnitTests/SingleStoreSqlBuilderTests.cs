using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreSqlBuilderTests : IDisposable
{
#if NET
    private const string NonNullableRefType = "NOT NULL";
#else
    // NRT annotations are only readable on .NET 6+; elsewhere reference types are assumed nullable.
    private const string NonNullableRefType = "NULL";
#endif

    private readonly SingleStoreConnection _connection = new("Host=localhost;Database=testdb;");

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void OptimizeTableFlush_QuotesDatabaseAndTable()
    {
        using var command = SingleStoreSqlBuilder.OptimizeTableFlush(_connection, "testdb", "hotels");

        Assert.Equal("OPTIMIZE TABLE `testdb`.`hotels` FLUSH", command.CommandText);
    }

    [Fact]
    public void ShowTables_WithoutTable_BuildsShowTablesFromDatabase()
    {
        using var command = SingleStoreSqlBuilder.ShowTables(_connection, "testdb");

        Assert.Equal("SHOW TABLES FROM `testdb`", command.CommandText);
        Assert.Empty(command.Parameters);
    }

    [Fact]
    public void ShowTables_WithTable_AddsLikeParameterAndEscapesWildcards()
    {
        using var command = SingleStoreSqlBuilder.ShowTables(_connection, "testdb", "hotel%_rooms");

        Assert.Equal("SHOW TABLES FROM `testdb` LIKE @table", command.CommandText);
        Assert.Single(command.Parameters);
        Assert.Equal("hotel\\%\\_rooms", command.Parameters["@table"].Value);
    }

    [Fact]
    public void DropTableIfExists_QuotesDatabaseAndTable()
    {
        using var command = SingleStoreSqlBuilder.DropTableIfExists(_connection, "testdb", "hotels");

        Assert.Equal("DROP TABLE IF EXISTS `testdb`.`hotels`", command.CommandText);
    }

    [Fact]
    public void Delete_QuotesIdentifiersAndBindsKey()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.Delete(_connection, "testdb", "hotels", model.KeyProperty, "h-1");

        Assert.Equal("DELETE FROM `testdb`.`hotels` WHERE `HotelId` = @key", command.CommandText);
        Assert.Single(command.Parameters);
        Assert.Equal("h-1", command.Parameters["@key"].Value);
    }

    [Fact]
    public void DeleteBatch_BuildsInClauseWithNumberedParameters()
    {
        var model = BuildHotelModel<int>();

        using var command = SingleStoreSqlBuilder.DeleteBatch(_connection, "testdb", "hotels", model.KeyProperty, [1, 2, 3]);

        Assert.Equal("DELETE FROM `testdb`.`hotels` WHERE `HotelId` IN (@key0, @key1, @key2)", command.CommandText);
        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal(1, command.Parameters["@key0"].Value);
        Assert.Equal(2, command.Parameters["@key1"].Value);
        Assert.Equal(3, command.Parameters["@key2"].Value);
    }

    [Fact]
    public void Select_IncludesAllColumnsAndKeyParameter_WhenIncludeVectorsIsTrue()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.Select(_connection, "testdb", "hotels", model, "id", true);

        Assert.Equal(
            "SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding` FROM `testdb`.`hotels` WHERE `HotelId` = @key",
            command.CommandText);
        Assert.Equal("id", command.Parameters["@key"].Value);
    }

    [Fact]
    public void Select_OmitsVectorColumns_WhenIncludeVectorsIsFalse()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.Select(_connection, "testdb", "hotels", model, "id", false);

        Assert.Equal(
            "SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt` FROM `testdb`.`hotels` WHERE `HotelId` = @key",
            command.CommandText);
        Assert.Equal("id", command.Parameters["@key"].Value);
    }

    [Fact]
    public void SelectBatch_IncludesAllColumnsAndNumberedKeyParameters_WhenIncludeVectorsIsTrue()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.SelectBatch(_connection, "testdb", "hotels", model, ["h-1", "h-2", "h-3"], true);

        Assert.Equal(
            "SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding` FROM `testdb`.`hotels` WHERE `HotelId` IN (@key0, @key1, @key2)",
            command.CommandText);
        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal("h-1", command.Parameters["@key0"].Value);
        Assert.Equal("h-2", command.Parameters["@key1"].Value);
        Assert.Equal("h-3", command.Parameters["@key2"].Value);
    }

    [Fact]
    public void SelectBatch_OmitsVectorColumns_WhenIncludeVectorsIsFalse()
    {
        var model = BuildHotelModel<int>();

        using var command = SingleStoreSqlBuilder.SelectBatch(_connection, "testdb", "hotels", model, [1, 2], false);

        Assert.Equal(
            "SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt` FROM `testdb`.`hotels` WHERE `HotelId` IN (@key0, @key1)",
            command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(1, command.Parameters["@key0"].Value);
        Assert.Equal(2, command.Parameters["@key1"].Value);
    }

    [Fact]
    public void SelectWhere_OmitsVectorColumnsAndAppliesLimitOffset()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.SelectWhere(
            _connection,
            "testdb",
            "hotels",
            model,
            r => r.HotelCode == 1,
            10,
            new FilteredRecordRetrievalOptions<SingleStoreHotel<string>>());

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`
                FROM `testdb`.`hotels`
                WHERE (`HotelCode` = 1)

                LIMIT 10 OFFSET 0
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Empty(command.Parameters);
    }

    [Fact]
    public void SelectVectorSearch_OmitsVectorColumns_WhenIncludeVectorsIsFalse()
    {
        var model = BuildHotelModel<string>();
        var vector = new[] { 0.1f, 0.2f, 0.3f, 0.4f };

        using var command = SingleStoreSqlBuilder.SelectVectorSearch(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            vector,
            (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
            0,
            false,
            10);

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding` <-> @vector AS sk_s2_distance, sk_s2_distance AS sk_s2_score
                FROM `testdb`.`hotels`

                ORDER BY sk_s2_distance ASC
                LIMIT 10 OFFSET 0
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Single(command.Parameters);
        Assert.Same(vector, command.Parameters["@vector"].Value);
    }

    [Fact]
    public void SelectVectorSearch_IncludesVectorColumnsFilterSkipAndParameters()
    {
        var model = BuildHotelModel<string>();
        var vector = new[] { 1f, 2f, 3f, 4f };
        var name = "Hilton";

        using var command = SingleStoreSqlBuilder.SelectVectorSearch<SingleStoreHotel<string>>(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            vector,
            r => r.HotelName == name,
            5,
            true,
            3);

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding`, `DescriptionEmbedding` <-> @vector AS sk_s2_distance, sk_s2_distance AS sk_s2_score
                FROM `testdb`.`hotels`
                WHERE (`HotelName` = @filter0)
                ORDER BY sk_s2_distance ASC
                LIMIT 3 OFFSET 5
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Same(vector, command.Parameters["@vector"].Value);
        Assert.Equal("Hilton", command.Parameters["@filter0"].Value);
    }

    [Theory]
    [InlineData(DistanceFunction.EuclideanDistance, "<->", "ASC", "sk_s2_distance", "<=")]
    [InlineData(DistanceFunction.EuclideanSquaredDistance, "<->", "ASC", "POW(sk_s2_distance, 2)", "<=")]
    [InlineData(DistanceFunction.DotProductSimilarity, "<*>", "DESC", "sk_s2_distance", ">=")]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, "<*>", "DESC", "-(sk_s2_distance)", "<=")]
    public void SelectVectorSearch_SupportedDistanceFunctions_MapOperatorOrderScoreAndThreshold(
        string distanceFunction,
        string expectedOperator,
        string expectedOrder,
        string expectedScore,
        string expectedThresholdSign)
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = distanceFunction;
        var vector = new[] { 0.5f, 0.5f, 0.5f, 0.5f };

        using var command = SingleStoreSqlBuilder.SelectVectorSearch(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            vector,
            (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
            0,
            false,
            2,
            0.25);

        Assert.Equal(
            $"""
                 SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding` {expectedOperator} @vector AS sk_s2_distance, {expectedScore} AS sk_s2_score
                 FROM `testdb`.`hotels`
                 WHERE sk_s2_score {expectedThresholdSign} @scoreThreshold
                 ORDER BY sk_s2_distance {expectedOrder}
                 LIMIT 2 OFFSET 0
                 """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(0.25, command.Parameters["@scoreThreshold"].Value);
        Assert.Same(vector, command.Parameters["@vector"].Value);
    }

    [Fact]
    public void SelectVectorSearch_NullDistanceFunction_UsesDotProductOperatorAndDescendingOrder()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = null;
        var vector = new[] { 1f, 0f, 0f, 0f };

        using var command = SingleStoreSqlBuilder.SelectVectorSearch(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            vector,
            (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
            1,
            false,
            4);

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding` <*> @vector AS sk_s2_distance, sk_s2_distance AS sk_s2_score
                FROM `testdb`.`hotels`

                ORDER BY sk_s2_distance DESC
                LIMIT 4 OFFSET 1
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Same(vector, command.Parameters["@vector"].Value);
    }

    [Fact]
    public void SelectVectorSearch_ScoreThresholdAndFilter_CombinesWhereClause()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = DistanceFunction.DotProductSimilarity;
        var vector = new[] { 0.1f, 0.2f, 0.3f, 0.4f };

        using var command = SingleStoreSqlBuilder.SelectVectorSearch<SingleStoreHotel<string>>(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            vector,
            r => r.HotelCode == 1,
            0,
            false,
            5,
            0.8);

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding` <*> @vector AS sk_s2_distance, sk_s2_distance AS sk_s2_score
                FROM `testdb`.`hotels`
                WHERE (`HotelCode` = 1) AND sk_s2_score >= @scoreThreshold
                ORDER BY sk_s2_distance DESC
                LIMIT 5 OFFSET 0
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(0.8, command.Parameters["@scoreThreshold"].Value);
        Assert.Same(vector, command.Parameters["@vector"].Value);
    }

    [Fact]
    public void SelectVectorSearch_UnsupportedDistanceFunction_Throws()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = DistanceFunction.ManhattanDistance;

        var exception = Assert.Throws<NotSupportedException>(() =>
            SingleStoreSqlBuilder.SelectVectorSearch(
                _connection,
                "testdb",
                "hotels",
                model,
                model.VectorProperties[0],
                new[] { 1f, 2f, 3f, 4f },
                (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
                0,
                false,
                1));

        Assert.Contains(DistanceFunction.ManhattanDistance, exception.Message);
    }

    [Fact]
    public void SelectHybridSearch_OmitsVectorColumnsAndJoinsRankedSubqueries()
    {
        var model = BuildHotelModel<string>();
        var vector = new[] { 0.1f, 0.2f, 0.3f, 0.4f };

        using var command = SingleStoreSqlBuilder.SelectHybridSearch(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            model.DataProperties.Single(property => property.StorageName == "Description"),
            vector,
            ["luxury", "spa"],
            (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
            0,
            false,
            3);

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`,
                COALESCE(1.0/(60+sk_s2_semantik_search.sk_s2_hybrid_search_rank), 0.0) + COALESCE(1.0/(60+sk_s2_keyword_search.sk_s2_hybrid_search_rank), 0.0)  AS sk_s2_score
                FROM
                (
                SELECT sk_s2_hybrid_search_id, RANK() OVER (ORDER BY sk_s2_score ASC) as sk_s2_hybrid_search_rank
                FROM (
                SELECT `HotelId` AS sk_s2_hybrid_search_id, `DescriptionEmbedding` <-> @vector AS sk_s2_score
                FROM `testdb`.`hotels`

                ORDER BY sk_s2_score ASC
                LIMIT 20)

                ) AS sk_s2_semantik_search
                FULL OUTER JOIN
                (
                SELECT sk_s2_hybrid_search_id, RANK() OVER (ORDER BY sk_s2_score DESC) as sk_s2_hybrid_search_rank
                FROM (
                SELECT `HotelId` AS sk_s2_hybrid_search_id, BM25(`testdb`.`hotels`, @BM25exp) AS sk_s2_score
                FROM `testdb`.`hotels`
                WHERE MATCH (TABLE `testdb`.`hotels`) AGAINST (@BM25exp)
                ORDER BY sk_s2_score DESC
                LIMIT 20)

                ) AS sk_s2_keyword_search
                ON sk_s2_semantik_search.sk_s2_hybrid_search_id = sk_s2_keyword_search.sk_s2_hybrid_search_id
                JOIN `testdb`.`hotels`
                ON `testdb`.`hotels`.`HotelId` = COALESCE(sk_s2_semantik_search.sk_s2_hybrid_search_id, sk_s2_keyword_search.sk_s2_hybrid_search_id)

                ORDER BY sk_s2_score DESC
                LIMIT 3 OFFSET 0
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Same(vector, command.Parameters["@vector"].Value);
        Assert.Equal("""Description:("luxury" "spa")""", command.Parameters["@BM25exp"].Value);
    }

    [Fact]
    public void SelectHybridSearch_FilterAndScoreThreshold_AppliedToBothSubqueriesAndOuterQuery()
    {
        var model = BuildHotelModel<string>();
        var vector = new[] { 1f, 2f, 3f, 4f };

        using var command = SingleStoreSqlBuilder.SelectHybridSearch<SingleStoreHotel<string>>(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            model.DataProperties.Single(property => property.StorageName == "HotelName"),
            vector,
            ["hilton"],
            r => r.HotelCode == 1,
            5,
            true,
            20,
            0.5);

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding`,
                COALESCE(1.0/(60+sk_s2_semantik_search.sk_s2_hybrid_search_rank), 0.0) + COALESCE(1.0/(60+sk_s2_keyword_search.sk_s2_hybrid_search_rank), 0.0)  AS sk_s2_score
                FROM
                (
                SELECT sk_s2_hybrid_search_id, RANK() OVER (ORDER BY sk_s2_score ASC) as sk_s2_hybrid_search_rank
                FROM (
                SELECT `HotelId` AS sk_s2_hybrid_search_id, `DescriptionEmbedding` <-> @vector AS sk_s2_score
                FROM `testdb`.`hotels`
                WHERE (`HotelCode` = 1)
                ORDER BY sk_s2_score ASC
                LIMIT 50)

                ) AS sk_s2_semantik_search
                FULL OUTER JOIN
                (
                SELECT sk_s2_hybrid_search_id, RANK() OVER (ORDER BY sk_s2_score DESC) as sk_s2_hybrid_search_rank
                FROM (
                SELECT `HotelId` AS sk_s2_hybrid_search_id, BM25(`testdb`.`hotels`, @BM25exp) AS sk_s2_score
                FROM `testdb`.`hotels`
                WHERE (`HotelCode` = 1) AND MATCH (TABLE `testdb`.`hotels`) AGAINST (@BM25exp)
                ORDER BY sk_s2_score DESC
                LIMIT 50)

                ) AS sk_s2_keyword_search
                ON sk_s2_semantik_search.sk_s2_hybrid_search_id = sk_s2_keyword_search.sk_s2_hybrid_search_id
                JOIN `testdb`.`hotels`
                ON `testdb`.`hotels`.`HotelId` = COALESCE(sk_s2_semantik_search.sk_s2_hybrid_search_id, sk_s2_keyword_search.sk_s2_hybrid_search_id)
                WHERE sk_s2_score >= @scoreThreshold
                ORDER BY sk_s2_score DESC
                LIMIT 20 OFFSET 5
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal(0.5, command.Parameters["@scoreThreshold"].Value);
        Assert.Same(vector, command.Parameters["@vector"].Value);
        Assert.Equal("""HotelName:("hilton")""", command.Parameters["@BM25exp"].Value);
    }

    [Theory]
    [InlineData(DistanceFunction.EuclideanDistance, "<->", "ASC")]
    [InlineData(DistanceFunction.EuclideanSquaredDistance, "<->", "ASC")]
    [InlineData(DistanceFunction.DotProductSimilarity, "<*>", "DESC")]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, "<*>", "DESC")]
    [InlineData(null, "<*>", "DESC")]
    public void SelectHybridSearch_SupportedDistanceFunctions_MapOperatorAndOrderOfSemanticSubquery(
        string? distanceFunction,
        string expectedOperator,
        string expectedOrder)
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = distanceFunction;

        using var command = SingleStoreSqlBuilder.SelectHybridSearch(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            model.DataProperties.Single(property => property.StorageName == "Description"),
            new[] { 0.5f, 0.5f, 0.5f, 0.5f },
            ["spa"],
            (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
            0,
            false,
            3);

        Assert.Contains(
            $"SELECT `HotelId` AS sk_s2_hybrid_search_id, `DescriptionEmbedding` {expectedOperator} @vector AS sk_s2_score",
            command.CommandText);
        Assert.Contains(
            $"SELECT sk_s2_hybrid_search_id, RANK() OVER (ORDER BY sk_s2_score {expectedOrder}) as sk_s2_hybrid_search_rank",
            command.CommandText);
        Assert.Contains($"ORDER BY sk_s2_score {expectedOrder}\nLIMIT", command.CommandText);
    }

    [Fact]
    public void SelectHybridSearch_UnsupportedDistanceFunction_Throws()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = DistanceFunction.ManhattanDistance;

        var exception = Assert.Throws<NotSupportedException>(() =>
            SingleStoreSqlBuilder.SelectHybridSearch(
                _connection,
                "testdb",
                "hotels",
                model,
                model.VectorProperties[0],
                model.DataProperties.Single(property => property.StorageName == "Description"),
                new[] { 1f, 2f, 3f, 4f },
                ["spa"],
                (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
                0,
                false,
                3));

        Assert.Contains(DistanceFunction.ManhattanDistance, exception.Message);
    }

    [Fact]
    public void SelectHybridSearch_EscapesBm25SpecialCharactersInKeywords()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.SelectHybridSearch(
            _connection,
            "testdb",
            "hotels",
            model,
            model.VectorProperties[0],
            model.DataProperties.Single(property => property.StorageName == "parking_is_included"),
            new[] { 1f, 2f, 3f, 4f },
            ["""a "quoted" word""", @"back\slash"],
            (Expression<Func<SingleStoreHotel<string>, bool>>?)null,
            0,
            false,
            3);

        Assert.Equal(
            """parking_is_included:("a \"quoted\" word" "back\\slash")""",
            command.Parameters["@BM25exp"].Value);
    }

    [Fact]
    public void SelectWhere_IncludesVectorColumnsOrderByAndFilterParameters()
    {
        var model = BuildHotelModel<string>();
        var name = "Hilton";

        using var command = SingleStoreSqlBuilder.SelectWhere(
            _connection,
            "testdb",
            "hotels",
            model,
            r => r.HotelName == name,
            3,
            new FilteredRecordRetrievalOptions<SingleStoreHotel<string>>
            {
                IncludeVectors = true,
                Skip = 5,
                OrderBy = o => o.Ascending(r => r.HotelName).Descending(r => r.ParkingIncluded)
            });

        Assert.Equal(
            """
                SELECT `HotelId`, `HotelName`, `HotelCode`, `HotelRating`, `parking_is_included`, `Tags`, `Description`, `CreatedAt`, `UpdatedAt`, `DescriptionEmbedding`
                FROM `testdb`.`hotels`
                WHERE (`HotelName` = @filter0)
                ORDER BY `HotelName` ASC, `parking_is_included` DESC
                LIMIT 3 OFFSET 5
                """.Replace("\r\n", "\n"),
            command.CommandText);
        Assert.Single(command.Parameters);
        Assert.Equal("Hilton", command.Parameters["@filter0"].Value);
    }

    [Fact]
    public void CreateTable_FromHotel_BuildsExpectedSql()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model);

        Assert.Equal(
            $$"""
                  CREATE TABLE IF NOT EXISTS `testdb`.`hotels`
                  (
                    `HotelId` LONGTEXT NULL,
                    `HotelName` LONGTEXT NULL,
                    `HotelCode` INT NOT NULL,
                    `HotelRating` FLOAT NULL,
                    `parking_is_included` BOOL NOT NULL,
                    `Tags` JSON {{NonNullableRefType}},
                    `Description` LONGTEXT {{NonNullableRefType}},
                    `CreatedAt` DATETIME(6) NOT NULL,
                    `UpdatedAt` DATETIME(6) NOT NULL,
                    `DescriptionEmbedding` VECTOR(4, F32) NULL,
                    PRIMARY KEY (`HotelId`),
                    INDEX (`HotelCode`),
                    MULTI VALUE INDEX (`Tags`) INDEX_OPTIONS='{"TOKENIZER":"MATCH_ANY", "PATH":[]}',
                    FULLTEXT USING VERSION 2 (`HotelName`, `Description`),
                    VECTOR KEY (`DescriptionEmbedding`) INDEX_OPTIONS '{ "metric_type":"EUCLIDEAN_DISTANCE", "index_type":"HNSW_FLAT" }'
                  )
                  """.Replace("\r\n", "\n"),
            command.CommandText);
    }

    [Fact]
    public void CreateTable_WithoutVectorOptions_OmitsIndexOption()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = null;
        model.VectorProperties[0].IndexKind = null;

        using var command = SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model);

        Assert.Contains(
            "VECTOR KEY (`DescriptionEmbedding`)",
            command.CommandText);
        Assert.DoesNotContain("index_type", command.CommandText);
        Assert.DoesNotContain("metric_type", command.CommandText);
    }

    [Theory]
    [InlineData(IndexKind.Dynamic, "AUTO")]
    [InlineData(IndexKind.Flat, "FLAT")]
    [InlineData(IndexKind.IvfFlat, "IVF_FLAT")]
    [InlineData(IndexKind.Hnsw, "HNSW_FLAT")]
    public void CreateTable_SupportedIndexKinds_MapToSingleStoreIndexTypes(string indexKind, string expected)
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = null;
        model.VectorProperties[0].IndexKind = indexKind;

        using var command = SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model);

        Assert.Contains($"\"index_type\":\"{expected}\"", command.CommandText);
        Assert.DoesNotContain("metric_type", command.CommandText);
    }

    [Theory]
    [InlineData(DistanceFunction.DotProductSimilarity, "DOT_PRODUCT")]
    [InlineData(DistanceFunction.EuclideanDistance, "EUCLIDEAN_DISTANCE")]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, "DOT_PRODUCT")]
    [InlineData(DistanceFunction.EuclideanSquaredDistance, "EUCLIDEAN_DISTANCE")]
    public void CreateTable_SupportedDistanceFunctions_MapToSingleStoreDistanceFunctions(string distanceFunction, string expected)
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = distanceFunction;
        model.VectorProperties[0].IndexKind = null;

        using var command = SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model);

        Assert.Contains($"\"metric_type\":\"{expected}\"", command.CommandText);
        Assert.DoesNotContain("index_type", command.CommandText);
    }

    [Fact]
    public void CreateTable_UnsupportedDistanceFunction_Throws()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = DistanceFunction.ManhattanDistance;
        model.VectorProperties[0].IndexKind = null;

        var exception = Assert.Throws<NotSupportedException>(() =>
            SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model));

        Assert.Contains(DistanceFunction.ManhattanDistance, exception.Message);
    }

    [Fact]
    public void CreateTable_UnsupportedIndexKind_Throws()
    {
        var model = BuildHotelModel<string>();
        model.VectorProperties[0].DistanceFunction = null;
        model.VectorProperties[0].IndexKind = IndexKind.DiskAnn;

        var exception = Assert.Throws<NotSupportedException>(() =>
            SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model));

        Assert.Contains(IndexKind.DiskAnn, exception.Message);
    }

    [Fact]
    public void ConfigureBulkLoadColumns_ByteArray_LoadsViaUnhex()
    {
        var model = new SingleStoreModelBuilder().BuildDynamic(
            new VectorStoreCollectionDefinition
            {
                Properties =
                [
                    new VectorStoreKeyProperty("id", typeof(string)),
                    new VectorStoreDataProperty("payload", typeof(byte[])),
                    new VectorStoreDataProperty("name", typeof(string))
                ]
            },
            null);

        var loader = new SingleStoreBulkLoader(_connection);
        SingleStoreSqlBuilder.ConfigureBulkLoadColumns(loader, model);

        Assert.Equal(["`id`", "@blob1", "`name`"], loader.Columns);
        Assert.Equal(["`payload` = UNHEX(@blob1)"], loader.Expressions);
    }

    private static CollectionModel BuildHotelModel<TKey>(VectorStoreCollectionDefinition? definition = null)
    {
        return new SingleStoreModelBuilder().Build(
            typeof(SingleStoreHotel<TKey>),
            typeof(TKey),
            definition,
            null);
    }
}
