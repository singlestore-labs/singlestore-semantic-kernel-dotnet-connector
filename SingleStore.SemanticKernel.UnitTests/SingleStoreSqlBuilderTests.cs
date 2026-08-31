using System;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreSqlBuilderTests : IDisposable
{
    private readonly SingleStoreConnection _connection = new("Host=localhost;Database=testdb;");

    public void Dispose()
    {
        _connection.Dispose();
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
    public void CreateTable_FromHotel_BuildsExpectedSql()
    {
        var model = BuildHotelModel<string>();

        using var command = SingleStoreSqlBuilder.CreateTable(_connection, "testdb", "hotels", model);

        Assert.Equal(
            """
            CREATE TABLE IF NOT EXISTS `testdb`.`hotels`
            (
              `HotelId` LONGTEXT NULL,
              `HotelName` LONGTEXT NULL,
              `HotelCode` INT NOT NULL,
              `HotelRating` FLOAT NULL,
              `parking_is_included` BOOL NOT NULL,
              `Tags` JSON NOT NULL,
              `Description` LONGTEXT NOT NULL,
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

        Assert.Equal(["`id`", "`hex_payload`", "`name`"], loader.Columns);
        Assert.Equal(["`payload` = UNHEX(`hex_payload`)"], loader.Expressions);
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
