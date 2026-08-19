using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Provides methods to build SQL commands for managing vector store collections in SingleStore.
/// </summary>
internal static class SingleStoreSqlBuilder
{
    private static readonly SingleStoreCommandBuilder Builder = new();

    private static string EscapeStringForLike(string value)
    {
        return value.Replace("%", "\\%").Replace("_", "\\_");
    }

    internal static SingleStoreCommand ShowTables(SingleStoreConnection connection, string database, string? table = null)
    {
        var command = connection.CreateCommand();

        var quotedDatabase = Builder.QuoteIdentifier(database);
        command.CommandText = $"SHOW TABLES FROM {quotedDatabase}";

        if (table is not null)
        {
            command.CommandText += " LIKE @table";
            command.Parameters.AddWithValue("@table", EscapeStringForLike(table));
        }

        return command;
    }

    internal static SingleStoreCommand DropTableIfExists(SingleStoreConnection connection, string database, string table)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {QuoteTable(database, table)}";

        return command;
    }

    private static string QuoteTable(string database, string table)
    {
        var quotedDatabase = Builder.QuoteIdentifier(database);
        var quotedTable = Builder.QuoteIdentifier(table);

        return $"{quotedDatabase}.{quotedTable}";
    }

    internal static SingleStoreCommand CreateTable(SingleStoreConnection connection, string database, string table, CollectionModel model)
    {
        var command = connection.CreateCommand();
        var columns = new List<string>
        {
            MapKeyColumnToSql(model.KeyProperty)
        };
        var keys = new List<string>
        {
            MapKeyToSql("PRIMARY KEY", model.KeyProperty)
        };

        var fullTextIndexedColumns = new List<PropertyModel>();
        foreach (var dataProperty in model.DataProperties)
        {
            columns.Add(MapColumnToSql(dataProperty));
            if (dataProperty.IsIndexed)
            {
                keys.Add(MapKeyToSql("INDEX", dataProperty));
            }

            if (dataProperty.IsFullTextIndexed)
            {
                fullTextIndexedColumns.Add(dataProperty);
            }
        }

        if (fullTextIndexedColumns.Count > 0)
        {
            keys.Add(MapKeyToSql("FULLTEXT USING VERSION 2", fullTextIndexedColumns.ToArray()));
        }

        foreach (var vectorProperty in model.VectorProperties)
        {
            columns.Add(MapVectorColumnToSql(vectorProperty));
            keys.Add(MapVectorKeyToSql(vectorProperty));
        }

        command.CommandText = $"CREATE TABLE IF NOT EXISTS {QuoteTable(database, table)}\n" +
                              $"(\n  " +
                              $"{string.Join(",\n  ", columns)}" +
                              ",\n  " +
                              $"{string.Join(",\n  ", keys)}" +
                              "\n)";
        return command;
    }

    private static string MapVectorKeyToSql(VectorPropertyModel property)
    {
        var key = MapKeyToSql("VECTOR KEY", property);
        var options = new List<string>();

        if (property.DistanceFunction is not null)
        {
            var metricType = MapDistanceFunctionToSql(property.DistanceFunction);
            options.Add($"\"metric_type\":\"{metricType}\"");
        }

        if (property.IndexKind is not null)
        {
            var indexType = MapIndexTypeToSql(property.IndexKind);
            options.Add($"\"index_type\":\"{indexType}\"");
        }

        return options.Count == 0
            ? key
            : $"{key} INDEX_OPTIONS '{{ {string.Join(", ", options)} }}'";
    }

    private static string MapDistanceFunctionToSql(string distanceFunction)
    {
        return distanceFunction switch
        {
            DistanceFunction.EuclideanDistance => "EUCLIDEAN_DISTANCE",
            DistanceFunction.DotProductSimilarity => "DOT_PRODUCT",
            _ => throw new NotSupportedException($"Distance function {distanceFunction} is not supported by this store.")
        };
    }

    private static string MapIndexTypeToSql(string indexKind)
    {
        return indexKind switch
        {
            IndexKind.Dynamic => "AUTO",
            IndexKind.Flat => "FLAT",
            IndexKind.IvfFlat => "IVF_FLAT",
            IndexKind.Hnsw => "HNSW_FLAT",
            _ => throw new NotSupportedException($"Index kind {indexKind} is not supported by this store.")
        };
    }

    private static string MapKeyToSql(string keyType, params PropertyModel[] properties)
    {
        var quotedColumnNames = Array.ConvertAll(properties,
            property =>
                Builder.QuoteIdentifier(property.StorageName)
        );

        return $"{keyType} ({string.Join(", ", quotedColumnNames)})";
    }


    private static string MapVectorColumnToSql(VectorPropertyModel property)
    {
        var quotedColumnName = Builder.QuoteIdentifier(property.StorageName);
        var nullability = property.IsNullable ? " NULL" : " NOT NULL";
        var type = MapVectorTypeToSql(property);

        return $"{quotedColumnName} {type}{nullability}";
    }

    private static string MapKeyColumnToSql(KeyPropertyModel property)
    {
        var columnDef = MapColumnToSql(property);
        var autoIncrement = property.IsAutoGenerated ? " AUTO_INCREMENT" : "";

        return $"{columnDef}{autoIncrement}";
    }

    private static string MapColumnToSql(PropertyModel property)
    {
        var quotedColumnName = Builder.QuoteIdentifier(property.StorageName);
        var type = MapTypeToSql(property);
        var nullability = property.IsNullable ? " NULL" : " NOT NULL";

        return $"{quotedColumnName} {type}{nullability}";
    }

    private static string MapVectorTypeToSql(VectorPropertyModel property)
    {
        var t = Nullable.GetUnderlyingType(property.EmbeddingType) ?? property.EmbeddingType;
        var elementType = t switch
        {
            not null when t == typeof(ReadOnlyMemory<float>)
                          || t == typeof(Embedding<float>)
                          || t == typeof(float[])
                => "F32",
            not null when t == typeof(ReadOnlyMemory<double>)
                          || t == typeof(Embedding<double>)
                          || t == typeof(double[])
                => "F64",
            not null when t == typeof(ReadOnlyMemory<sbyte>)
                          || t == typeof(Embedding<sbyte>)
                          || t == typeof(sbyte[])
                => "I8",
            not null when t == typeof(ReadOnlyMemory<short>)
                          || t == typeof(Embedding<short>)
                          || t == typeof(short[])
                => "I16",
            not null when t == typeof(ReadOnlyMemory<int>)
                          || t == typeof(Embedding<int>)
                          || t == typeof(int[])
                => "I32",
            not null when t == typeof(ReadOnlyMemory<long>)
                          || t == typeof(Embedding<long>)
                          || t == typeof(long[])
                => "I64",
            _ => throw new NotSupportedException($"Type {property.EmbeddingType.Name} is not supported by this store.")
        };

        return $"VECTOR({property.Dimensions}, {elementType})";
    }

    private static string MapTypeToSql(PropertyModel property)
    {
        var t = Nullable.GetUnderlyingType(property.Type) ?? property.Type;

        return t switch
        {
            not null when t == typeof(bool) => "BOOL",
            not null when t == typeof(byte) => "TINYINT UNSIGNED",
            not null when t == typeof(sbyte) => "TINYINT",
            not null when t == typeof(short) => "SMALLINT",
            not null when t == typeof(ushort) => "SMALLINT UNSIGNED",
            not null when t == typeof(int) => "INT",
            not null when t == typeof(uint) => "INT UNSIGNED",
            not null when t == typeof(long) => "BIGINT",
            not null when t == typeof(ulong) => "BIGINT UNSIGNED",
            not null when t == typeof(float) => "FLOAT",
            not null when t == typeof(double) => "DOUBLE",
            not null when t == typeof(decimal) => "DECIMAL(65,30)",
            not null when t == typeof(string) => "LONGTEXT",
            not null when t == typeof(byte[]) => "LONGBLOB",
            not null when t == typeof(DateTime) => "DATETIME(6)",
            not null when t == typeof(DateTimeOffset) => "DATETIME(6)",
#if NET
            not null when t == typeof(DateOnly) => "DATE",
            not null when t == typeof(TimeOnly) => "TIME(6)",
#endif
            // TODO: SingleStore does not have a dedicated GUID type. Verify that GUID values are written and read correctly.
            not null when t == typeof(Guid) => "BINARY(16)",
            not null when t == typeof(string[]) || t == typeof(List<string>) => "JSON",
            _ => throw new NotSupportedException($"Type {property.Type} is not supported.")
        };
    }
}
