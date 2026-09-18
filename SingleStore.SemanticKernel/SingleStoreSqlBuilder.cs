using System.Globalization;
using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// Provides methods to build SQL commands for managing vector store collections in SingleStore.
/// </summary>
internal static class SingleStoreSqlBuilder
{
    internal static readonly SingleStoreCommandBuilder Builder = new();

    private static string EscapeStringForLike(string value)
    {
        return value.Replace("%", "\\%").Replace("_", "\\_");
    }

    internal static SingleStoreCommand Delete<TKey>(SingleStoreConnection connection,
        string database,
        string table,
        KeyPropertyModel property,
        TKey key)
    {
        var command = connection.CreateCommand();

        var quotedTable = QuoteTable(database, table);
        var quotedKeyColumn = Builder.QuoteIdentifier(property.StorageName);
        command.CommandText = $"DELETE FROM {quotedTable} WHERE {quotedKeyColumn} = @key";
        command.Parameters.AddWithValue("@key", key);

        return command;
    }

    internal static SingleStoreCommand SelectBatch<TKey>(SingleStoreConnection connection,
        string database,
        string table,
        CollectionModel model,
        List<TKey> keys,
        bool includeVectors)
    {
        var command = connection.CreateCommand();

        var counter = 0;
        foreach (var key in keys)
        {
            var parameterName = $"@key{counter++}";
            command.Parameters.AddWithValue(parameterName, key);
        }

        var columns = MapColumnsToSql(model.Properties, includeVectors);
        var quotedTable = QuoteTable(database, table);
        var quotedKeyColumn = Builder.QuoteIdentifier(model.KeyProperty.StorageName);
        command.CommandText = $"SELECT {columns} FROM {quotedTable} WHERE {quotedKeyColumn} IN ({string.Join(", ", command.Parameters.Select(parameter => parameter.ParameterName))})";

        return command;
    }

    internal static SingleStoreCommand Select<TKey>(SingleStoreConnection connection,
        string database,
        string table,
        CollectionModel model,
        TKey key,
        bool includeVectors)
    {
        var command = connection.CreateCommand();

        var columns = MapColumnsToSql(model.Properties, includeVectors);
        var quotedTable = QuoteTable(database, table);
        var quotedKeyColumn = Builder.QuoteIdentifier(model.KeyProperty.StorageName);
        command.CommandText = $"SELECT {columns} FROM {quotedTable} WHERE {quotedKeyColumn} = @key";
        command.Parameters.AddWithValue("@key", key);

        return command;
    }

    internal static SingleStoreCommand SelectWhere<TRecord>(SingleStoreConnection connection,
        string database,
        string table,
        CollectionModel model,
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord> options)
    {
        var command = connection.CreateCommand();

        var columns = MapColumnsToSql(model.Properties, options.IncludeVectors);
        var quotedTable = QuoteTable(database, table);
        var (whereClause, whereClauseParameters) = MapFilterConditionToSql(model, filter);
        var orderByValues = options.OrderBy?.Invoke(new FilteredRecordRetrievalOptions<TRecord>.OrderByDefinition()).Values;
        var orderByClause = orderByValues == null || orderByValues.Count == 0
            ? ""
            : $"ORDER BY {
                string.Join(", ", orderByValues.Select(sortInfo => MapSortInfoToSql(sortInfo, model)))
            }";

        command.CommandText = $"SELECT {columns}\n" +
                              $"FROM {quotedTable}\n" +
                              $"{whereClause}\n" +
                              $"{orderByClause}\n" +
                              $"LIMIT {top} OFFSET {options.Skip}";

        foreach (var parameter in whereClauseParameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    internal static SingleStoreCommand SelectVectorSearch<TRecord>(SingleStoreConnection connection,
        string database,
        string table,
        CollectionModel model,
        VectorPropertyModel property,
        object vectorValue,
        Expression<Func<TRecord, bool>>? filter,
        int skip,
        bool includeVectors,
        int top,
        double? scoreThreshold = null)
    {
        var command = connection.CreateCommand();

        var columns = MapColumnsToSql(model.Properties, includeVectors);
        var quotedVectorColumn = Builder.QuoteIdentifier(property.StorageName);
        var (vectorOperator, order, score, thresholdSign) = MapVectorSearchToSql(property.DistanceFunction);
        var (whereClause, whereClauseParameters) = MapFilterConditionToSql(model, filter);
        var quotedTableName = QuoteTable(database, table);

        if (scoreThreshold.HasValue)
        {
            if (whereClause.Length == 0)
            {
                whereClause = $"WHERE {SingleStoreConstants.ScoreColumnName} {thresholdSign} @scoreThreshold";
            }
            else
            {
                whereClause = $"{whereClause} AND {SingleStoreConstants.ScoreColumnName} {thresholdSign} @scoreThreshold";
            }

            command.Parameters.Add(new SingleStoreParameter("@scoreThreshold", scoreThreshold.Value));
        }

        command.CommandText = $"SELECT {columns}, {quotedVectorColumn} {vectorOperator} @vector AS {SingleStoreConstants.DistanceColumnName}, {score} AS {SingleStoreConstants.ScoreColumnName}\n" +
                              $"FROM {quotedTableName}\n" +
                              $"{whereClause}\n" +
                              $"ORDER BY {SingleStoreConstants.DistanceColumnName} {order}\n" +
                              $"LIMIT {top} OFFSET {skip}";

        command.Parameters.Add(new SingleStoreParameter("@vector", vectorValue));
        foreach (var parameter in whereClauseParameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    internal static SingleStoreCommand DeleteBatch<TKey>(SingleStoreConnection connection,
        string database,
        string table,
        KeyPropertyModel property,
        List<TKey> keys)
    {
        var command = connection.CreateCommand();

        var counter = 0;
        foreach (var key in keys)
        {
            var parameterName = $"@key{counter++}";
            command.Parameters.AddWithValue(parameterName, key);
        }

        var quotedTable = QuoteTable(database, table);
        var quotedKeyColumn = Builder.QuoteIdentifier(property.StorageName);
        command.CommandText = $"DELETE FROM {quotedTable} WHERE {quotedKeyColumn} IN ({string.Join(", ", command.Parameters.Select(parameter => parameter.ParameterName))})";

        return command;
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

    internal static string QuoteTable(string database, string table)
    {
        var quotedDatabase = Builder.QuoteIdentifier(database);
        var quotedTable = Builder.QuoteIdentifier(table);

        return $"{quotedDatabase}.{quotedTable}";
    }

    internal static void ConfigureBulkLoadColumns(SingleStoreBulkLoader loader, CollectionModel model)
    {
        for (var i = 0; i < model.Properties.Count; i++)
        {
            var property = model.Properties[i];
            var quoted = Builder.QuoteIdentifier(property.StorageName);
            if (property.Type == typeof(byte[]))
            {
                var variable = "@blob" + i.ToString(CultureInfo.InvariantCulture);
                loader.Columns.Add(variable);
                loader.Expressions.Add($"{quoted} = UNHEX({variable})");
            }
            else
            {
                loader.Columns.Add(quoted);
            }
        }
    }

    internal static SingleStoreCommand CreateTable(SingleStoreConnection connection, string database, string table, CollectionModel model)
    {
        var command = connection.CreateCommand();
        var columns = new List<string>
        {
            MapColumnToSql(model.KeyProperty)
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
                if (dataProperty.Type == typeof(string[]) || dataProperty.Type == typeof(List<string>))
                {
                    keys.Add(MapJsonKeyToSql(dataProperty));
                }
                else
                {
                    keys.Add(MapKeyToSql("INDEX", dataProperty));
                }
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

    private static (string Operator, string Order, string Score, string ThresholdSign) MapVectorSearchToSql(string? distanceFunction)
    {
        return distanceFunction switch
        {
            DistanceFunction.EuclideanDistance =>
                new ValueTuple<string, string, string, string>("<->", "ASC", SingleStoreConstants.DistanceColumnName, "<="),
            DistanceFunction.EuclideanSquaredDistance =>
                new ValueTuple<string, string, string, string>("<->", "ASC", $"POW({SingleStoreConstants.DistanceColumnName}, 2)", "<="),
            DistanceFunction.NegativeDotProductSimilarity =>
                new ValueTuple<string, string, string, string>("<*>", "DESC", $"-({SingleStoreConstants.DistanceColumnName})", "<="),
            DistanceFunction.DotProductSimilarity or null =>
                new ValueTuple<string, string, string, string>("<*>", "DESC", SingleStoreConstants.DistanceColumnName, ">="),
            _ => throw new NotSupportedException(
                $"Distance function {distanceFunction} is not supported by this store.")
        };
    }

    private static string MapColumnsToSql(IReadOnlyList<PropertyModel> columns, bool includeVectors)
    {
        return string.Join(", ",
            columns
                .Where(p => includeVectors || !(p is VectorPropertyModel))
                .Select(p => Builder.QuoteIdentifier(p.StorageName)).ToList());
    }

    private static (string Condition, List<SingleStoreParameter> Parameters) MapFilterConditionToSql(CollectionModel model, LambdaExpression? filter)
    {
        if (filter is null)
        {
            return (string.Empty, Parameters: new List<SingleStoreParameter>());
        }

        SingleStoreFilterTranslator translator = new(model, filter);
        translator.Translate(true);
        return (translator.Clause.ToString(), translator.Parameters);
    }

    private static string MapSortInfoToSql<TRecord>(FilteredRecordRetrievalOptions<TRecord>.OrderByDefinition.SortInfo sortInfo, CollectionModel model)
    {
        var column = Builder.QuoteIdentifier(model.GetDataOrKeyProperty(sortInfo.PropertySelector).StorageName);
        var direction = sortInfo.Ascending ? "ASC" : "DESC";
        return $"{column} {direction}";
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
            DistanceFunction.EuclideanSquaredDistance => "EUCLIDEAN_DISTANCE",
            DistanceFunction.NegativeDotProductSimilarity => "DOT_PRODUCT",
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

    private static string MapJsonKeyToSql(PropertyModel property)
    {
        return $"{MapKeyToSql("MULTI VALUE INDEX", property)} INDEX_OPTIONS='{{\"TOKENIZER\":\"MATCH_ANY\", \"PATH\":[]}}'";
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

    private static string MapColumnToSql(PropertyModel property)
    {
        var quotedColumnName = Builder.QuoteIdentifier(property.StorageName);
        var type = MapTypeToSql(property);
        var nullability = property.IsNullable ? " NULL" : " NOT NULL";

        return $"{quotedColumnName} {type}{nullability}";
    }

    private static string MapVectorTypeToSql(VectorPropertyModel property)
    {
        return $"VECTOR({property.Dimensions}, F32)";
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
            not null when t == typeof(Guid) => "CHAR(36)",
            not null when t == typeof(string[]) || t == typeof(List<string>) => "JSON",
            _ => throw new NotSupportedException($"Type {property.Type} is not supported.")
        };
    }
}
