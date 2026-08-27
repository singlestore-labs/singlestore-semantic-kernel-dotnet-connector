using System.Collections;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

internal sealed class SingleStoreFilterTranslator : SqlFilterTranslator
{
    private int _parameterIndex;

    internal SingleStoreFilterTranslator(
        CollectionModel model,
        LambdaExpression lambdaExpression,
        int startParamIndex,
        StringBuilder? sql = null) : base(model, lambdaExpression, sql)
    {
        _parameterIndex = startParamIndex;
    }

    internal List<SingleStoreParameter> Parameters { get; } = [];

    protected override void GenerateColumn(PropertyModel property, bool isSearchCondition = false)
    {
        _sql.Append(SingleStoreSqlBuilder.Builder.QuoteIdentifier(property.StorageName));
    }

    protected override void TranslateConstant(object? value, bool isSearchCondition)
    {
        switch (value)
        {
            case bool boolValue when isSearchCondition:
                _sql.Append(boolValue ? "1 = 1" : "1 = 0");
                return;
            case bool boolValue:
                _sql.Append(boolValue ? "1 :> TINYINT" : "0 :> TINYINT");
                return;
            case DateTime dateTime:
                _sql.Append('\'').Append(dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff")).Append('\'');
                return;
            case DateTimeOffset dateTimeOffset:
                if (dateTimeOffset.Offset != TimeSpan.Zero)
                {
                    throw new ArgumentException(
                        $"Cannot write DateTimeOffset with Offset={dateTimeOffset.Offset}, only offset 0 (UTC) is supported.",
                        nameof(value));
                }

                _sql.Append('\'').Append(dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.ffffff")).Append('\'');
                return;
            case string[] stringArray:
                _sql.Append('\'').Append(JsonSerializer.Serialize(stringArray).Replace("'", "''")).Append('\'');
                return;
            case List<string> stringList:
                _sql.Append('\'').Append(JsonSerializer.Serialize(stringList).Replace("'", "''")).Append('\'');
                return;

#if NET
            case DateOnly dateOnly:
                _sql.Append('\'').Append(dateOnly.ToString("yyyy-MM-dd")).Append('\'');
                return;
            case TimeOnly timeOnly:
                _sql.Append('\'').Append(timeOnly.ToString("HH:mm:ss.ffffff")).Append('\'');
                return;
#endif

            default:
                base.TranslateConstant(value, isSearchCondition);
                break;
        }
    }


    protected override void TranslateContainsOverArrayColumn(Expression source, Expression item)
    {
        if (item.Type != typeof(string))
        {
            throw new NotSupportedException("Unsupported Contains expression");
        }

        _sql.Append("JSON_MATCH_ANY(MATCH_PARAM_JSON() = TO_JSON(");
        Translate(item);
        _sql.Append("), ");
        Translate(source);
        _sql.Append(")");
    }

    protected override void TranslateContainsOverParameterizedArray(Expression source, Expression item, object? value)
    {
        if (value is not IEnumerable elements)
        {
            throw new NotSupportedException("Unsupported Contains expression");
        }

        Translate(item);
        _sql.Append(" IN (");

        var isFirst = true;
        foreach (var element in elements)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                _sql.Append(", ");
            }

            TranslateConstant(element, false);
        }

        _sql.Append(')');
    }

    protected override void TranslateAnyContainsOverArrayColumn(PropertyModel property, object? values)
    {
        // Translate r.Strings.Any(s => array.Contains(s)) to:
        // JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (TO_JSON('a'), TO_JSON('b')), column)
        if (values is not IEnumerable elements)
        {
            throw new NotSupportedException("Unsupported Any expression");
        }

        _sql.Append("JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (");

        var isFirst = true;
        foreach (var element in elements)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                _sql.Append(", ");
            }

            _sql.Append("TO_JSON(");
            TranslateConstant(element, false);
            _sql.Append(")");
        }

        _sql.Append("), ");
        GenerateColumn(property);
        _sql.Append(")");
    }


    protected override void TranslateQueryParameter(object? value)
    {
        // For null values, simply inline rather than parameterize;
        if (value is null)
        {
            _sql.Append("NULL");
        }
        else
        {
            var name = $"@filter{_parameterIndex++}";
            Parameters.Add(new SingleStoreParameter(name, value));
            _sql.Append(name);
        }
    }
}
