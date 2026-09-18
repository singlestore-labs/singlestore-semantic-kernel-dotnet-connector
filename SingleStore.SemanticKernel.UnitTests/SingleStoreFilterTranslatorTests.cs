using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreFilterTranslatorTests
{
    private readonly CollectionModel _integerModel = new SingleStoreModelBuilder().Build(typeof(IntegerRecord), typeof(string), null, null);
    private readonly CollectionModel _model = new SingleStoreModelBuilder().Build(typeof(SingleStoreHotel<string>), typeof(string), null, null);

    [Fact]
    public void Translate_Equal_ParameterizesCapturedValue()
    {
        var code = 42;
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.HotelCode == code);
        translator.Translate(true);

        Assert.Equal("WHERE (`HotelCode` = @filter0)", translator.Clause.ToString());
        Assert.Equal(42, Assert.Single(translator.Parameters).Value);
        Assert.Equal("@filter0", translator.Parameters[0].ParameterName);
    }

    [Fact]
    public void Translate_MultipleCapturedValues_UsesIncrementingParameterNames()
    {
        var code = 7;
        var name = "Hilton";
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.HotelCode == code && r.HotelName == name);
        translator.Translate(true);

        Assert.Equal("WHERE ((`HotelCode` = @filter0) AND (`HotelName` = @filter1))", translator.Clause.ToString());
        Assert.Equal(2, translator.Parameters.Count);
        Assert.Equal(7, translator.Parameters[0].Value);
        Assert.Equal("Hilton", translator.Parameters[1].Value);
    }

    [Fact]
    public void Translate_BoolMember_QuotesStorageName()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.ParkingIncluded);
        translator.Translate(true);

        Assert.Equal("WHERE `parking_is_included`", translator.Clause.ToString());
    }

    [Fact]
    public void Translate_ConstantBool_AsSearchCondition()
    {
        var alwaysTrue = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => true);
        alwaysTrue.Translate(true);
        Assert.Equal("WHERE 1 = 1", alwaysTrue.Clause.ToString());

        var alwaysFalse = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => false);
        alwaysFalse.Translate(true);
        Assert.Equal("WHERE 1 = 0", alwaysFalse.Clause.ToString());
    }

    [Fact]
    public void Translate_BoolEquality_UsesTinyintCast()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.ParkingIncluded == true);
        translator.Translate(true);

        Assert.Equal("WHERE (`parking_is_included` = 1 :> TINYINT)", translator.Clause.ToString());
    }

    [Fact]
    public void Translate_ContainsOverArrayColumn_UsesJsonMatchAny()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Contains("pool"));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() = TO_JSON('pool'), `Tags`)", translator.Clause.ToString());
    }

    [Fact]
    public void Translate_ContainsOverArrayColumn_ParameterizesCapturedItem()
    {
        var tag = "pool";
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Contains(tag));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() = TO_JSON(@filter0), `Tags`)", translator.Clause.ToString());
        Assert.Equal("pool", Assert.Single(translator.Parameters).Value);
    }

    [Fact]
    public void Translate_ContainsOverInlineArray_InlinesElements()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => new[] { "a", "b" }.Contains(r.HotelName));
        translator.Translate(true);

        Assert.Equal("WHERE `HotelName` IN ('a', 'b')", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_ContainsOverCapturedArray_ParameterizesElements()
    {
        var names = new[] { "a", "b" };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => names.Contains(r.HotelName));
        translator.Translate(true);

        Assert.Equal("WHERE `HotelName` IN (@filter0, @filter1)", translator.Clause.ToString());
        Assert.Equal(2, translator.Parameters.Count);
        Assert.Equal("a", translator.Parameters[0].Value);
        Assert.Equal("b", translator.Parameters[1].Value);
    }

    [Fact]
    public void Translate_ContainsOverCapturedEmptyCollection_MatchesNoRows()
    {
        var names = Array.Empty<string>();
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => names.Contains(r.HotelName));
        translator.Translate(true);

        Assert.Equal("WHERE 1 = 0", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_AnyContainsOverInlineArray_ParameterizesElements()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Any(t => new[] { "pool", "spa" }.Contains(t)));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (TO_JSON(@filter0), TO_JSON(@filter1)), `Tags`)", translator.Clause.ToString());
        Assert.Equal(2, translator.Parameters.Count);
        Assert.Equal("pool", translator.Parameters[0].Value);
        Assert.Equal("spa", translator.Parameters[1].Value);
    }

    [Fact]
    public void Translate_AnyContainsOverCapturedArray_ParameterizesElements()
    {
        var tags = new[] { "pool", "spa" };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Any(t => tags.Contains(t)));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (TO_JSON(@filter0), TO_JSON(@filter1)), `Tags`)", translator.Clause.ToString());
        Assert.Equal(2, translator.Parameters.Count);
        Assert.Equal("pool", translator.Parameters[0].Value);
        Assert.Equal("spa", translator.Parameters[1].Value);
    }

    [Fact]
    public void Translate_AnyContainsOverEmptyCollection_MatchesNoRows()
    {
        var tags = Array.Empty<string>();
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Any(t => tags.Contains(t)));
        translator.Translate(true);

        Assert.Equal("WHERE 1 = 0", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_AnyContainsOverInlineEmptyArray_MatchesNoRows()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Any(t => new string[] { }.Contains(t)));
        translator.Translate(true);

        Assert.Equal("WHERE 1 = 0", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_DateTimeConstant_UsesSingleStoreFormat()
    {
        var translator = new SingleStoreFilterTranslator(
            _model,
            (SingleStoreHotel<string> r) => r.CreatedAt == new DateTime(2024, 6, 15, 13, 4, 5, 123));
        translator.Translate(true);

        Assert.Equal("WHERE (`CreatedAt` = '2024-06-15 13:04:05.123000')", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_DateTimeOffsetUtcConstant_UsesSingleStoreFormat()
    {
        var translator = new SingleStoreFilterTranslator(
            _model,
            (SingleStoreHotel<string> r) => r.UpdatedAt == new DateTimeOffset(2024, 6, 15, 13, 4, 5, TimeSpan.FromHours(0)));
        translator.Translate(true);

        Assert.Equal("WHERE (`UpdatedAt` = '2024-06-15 13:04:05.000000')", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_ContainsOverCapturedDateTimes_ParameterizesElements()
    {
        var createdAt = new DateTime(2024, 6, 15, 13, 4, 5, 123);
        var dates = new[] { createdAt };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => dates.Contains(r.CreatedAt));
        translator.Translate(true);

        Assert.Equal("WHERE `CreatedAt` IN (@filter0)", translator.Clause.ToString());
        Assert.Equal(createdAt, Assert.Single(translator.Parameters).Value);
    }

    [Fact]
    public void Translate_ContainsOverCapturedDateTimeOffsets_ParameterizesElements()
    {
        var updatedAt = new DateTimeOffset(2024, 6, 15, 13, 4, 5, TimeSpan.Zero);
        var dates = new[] { updatedAt };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => dates.Contains(r.UpdatedAt));
        translator.Translate(true);

        Assert.Equal("WHERE `UpdatedAt` IN (@filter0)", translator.Clause.ToString());
        Assert.Equal(updatedAt, Assert.Single(translator.Parameters).Value);
    }

    [Fact]
    public void Translate_StringConstant_EscapesQuotesAndBackslashes()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.HotelName == "O'Brien\\suite");
        translator.Translate(true);

        Assert.Equal("WHERE (`HotelName` = 'O\\'Brien\\\\suite')", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_ContainsOverArrayColumn_EscapesStringLiteral()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Contains("O'Brien"));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() = TO_JSON('O\\'Brien'), `Tags`)", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_SByteConstant_InlinesInvariantValue()
    {
        var translator = new SingleStoreFilterTranslator(_integerModel, EqualToConstant<sbyte>(r => r.SByteValue, -8));
        translator.Translate(true);

        Assert.Equal("WHERE (`SByteValue` = -8)", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_UShortConstant_InlinesInvariantValue()
    {
        var translator = new SingleStoreFilterTranslator(_integerModel, EqualToConstant<ushort>(r => r.UShortValue, 65535));
        translator.Translate(true);

        Assert.Equal("WHERE (`UShortValue` = 65535)", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_UIntConstant_InlinesInvariantValue()
    {
        var translator = new SingleStoreFilterTranslator(_integerModel, (IntegerRecord r) => r.UIntValue == 4000000000u);
        translator.Translate(true);

        Assert.Equal("WHERE (`UIntValue` = 4000000000)", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_ULongConstant_InlinesInvariantValue()
    {
        var translator = new SingleStoreFilterTranslator(_integerModel, (IntegerRecord r) => r.ULongValue == 18446744073709551615ul);
        translator.Translate(true);

        Assert.Equal("WHERE (`ULongValue` = 18446744073709551615)", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
    }

    [Fact]
    public void Translate_DateTimeOffsetWithOffset_ThrowsWhenInlined()
    {
        var translator = new SingleStoreFilterTranslator(
            _model,
            (SingleStoreHotel<string> r) => r.UpdatedAt == new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)));

        var exception = Assert.Throws<ArgumentException>(() => translator.Translate(true));

        Assert.Contains("offset 0", exception.Message);
    }

    private static Expression<Func<IntegerRecord, bool>> EqualToConstant<T>(
        Expression<Func<IntegerRecord, T>> property,
        T value)
    {
        var parameter = property.Parameters[0];
        var body = Expression.Equal(property.Body, Expression.Constant(value, typeof(T)));
        return Expression.Lambda<Func<IntegerRecord, bool>>(body, parameter);
    }

    private sealed class IntegerRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = "";

        [VectorStoreData]
        public sbyte SByteValue { get; set; }

        [VectorStoreData]
        public ushort UShortValue { get; set; }

        [VectorStoreData]
        public uint UIntValue { get; set; }

        [VectorStoreData]
        public ulong ULongValue { get; set; }
    }
}
