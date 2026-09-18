using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

internal sealed class SingleStoreFilterTranslator : SqlFilterTranslator
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.ffffff";

    private int _parameterIndex;

    internal SingleStoreFilterTranslator(
        CollectionModel model,
        LambdaExpression lambdaExpression,
        StringBuilder? sql = null) : base(model, lambdaExpression, sql)
    {
    }

    internal List<SingleStoreParameter> Parameters { get; } = [];

    protected override void GenerateColumn(PropertyModel property, bool isSearchCondition = false)
    {
        _sql.Append(SingleStoreSqlBuilder.Builder.QuoteIdentifier(property.StorageName));
    }

    private string EscapeStringLiteral(string s)
    {
        return s.Replace("\\", "\\\\").Replace("'", "\\'");
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
                _sql.Append('\'').Append(dateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture)).Append('\'');
                return;
            case DateTimeOffset dateTimeOffset:
                if (dateTimeOffset.Offset != TimeSpan.Zero)
                {
                    throw new ArgumentException(
                        $"Cannot write DateTimeOffset with Offset={dateTimeOffset.Offset}, only offset 0 (UTC) is supported.",
                        nameof(value));
                }

                _sql.Append('\'').Append(dateTimeOffset.ToString(DateTimeFormat, CultureInfo.InvariantCulture)).Append('\'');
                return;
            case string[] stringArray:
                _sql.Append('\'').Append(EscapeStringLiteral(JsonSerializer.Serialize(stringArray))).Append('\'');
                return;
            case List<string> stringList:
                _sql.Append('\'').Append(EscapeStringLiteral(JsonSerializer.Serialize(stringList))).Append('\'');
                return;
            case string str:
                _sql.Append('\'').Append(EscapeStringLiteral(str)).Append('\'');
                return;

#if NET
            case DateOnly dateOnly:
                _sql.Append('\'').Append(dateOnly.ToString(DateFormat, CultureInfo.InvariantCulture)).Append('\'');
                return;
            case TimeOnly timeOnly:
                _sql.Append('\'').Append(timeOnly.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('\'');
                return;
#endif
            case float f:
                _sql.Append(f.ToString(CultureInfo.InvariantCulture));
                return;
            case double d:
                _sql.Append(d.ToString(CultureInfo.InvariantCulture));
                return;
            case decimal d:
                _sql.Append(d.ToString(CultureInfo.InvariantCulture));
                return;
            case sbyte sb:
                _sql.Append(sb.ToString(CultureInfo.InvariantCulture));
                return;
            case ushort us:
                _sql.Append(us.ToString(CultureInfo.InvariantCulture));
                return;
            case uint ui:
                _sql.Append(ui.ToString(CultureInfo.InvariantCulture));
                return;
            case ulong ul:
                _sql.Append(ul.ToString(CultureInfo.InvariantCulture));
                return;

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

        // JSON_MATCH_ANY(MATCH_PARAM_JSON() = TO_JSON(item), source)
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

        var items = elements.Cast<object?>().ToList();
        if (items.Count == 0)
        {
            _sql.Append("1 = 0");
            return;
        }

        // item IN (value[0], value[1], ...)
        Translate(item);
        _sql.Append(" IN (");

        var isFirst = true;
        foreach (var element in items)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                _sql.Append(", ");
            }

            TranslateQueryParameter(element);
        }

        _sql.Append(')');
    }

    protected override void TranslateAnyContainsOverArrayColumn(PropertyModel property, object? values)
    {
        // Translate r.Strings.Any(s => array.Contains(s)) to:
        // JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (TO_JSON(value[0]), TO_JSON(value[1]), ...), column)
        if (values is not IEnumerable elements)
        {
            throw new NotSupportedException("Unsupported Any expression");
        }

        var items = elements.Cast<object?>().ToList();
        if (items.Count == 0)
        {
            _sql.Append("1 = 0");
            return;
        }

        _sql.Append("JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (");

        var isFirst = true;
        foreach (var element in items)
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
            TranslateQueryParameter(element);
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
#if NET
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm:ss.ffffff";
#endif
}
