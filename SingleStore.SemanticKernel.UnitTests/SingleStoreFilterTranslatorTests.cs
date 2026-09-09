using System;
using System.Linq;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class SingleStoreFilterTranslatorTests
{
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
    public void Translate_ContainsOverCapturedArray_InlinesElements()
    {
        var names = new[] { "a", "b" };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => names.Contains(r.HotelName));
        translator.Translate(true);

        Assert.Equal("WHERE `HotelName` IN ('a', 'b')", translator.Clause.ToString());
        Assert.Empty(translator.Parameters);
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
    public void Translate_AnyContainsOverInlineArray_UsesJsonMatchAnyIn()
    {
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Any(t => new[] { "pool", "spa" }.Contains(t)));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (TO_JSON('pool'), TO_JSON('spa')), `Tags`)", translator.Clause.ToString());
    }

    [Fact]
    public void Translate_AnyContainsOverCapturedArray_UsesJsonMatchAnyIn()
    {
        var tags = new[] { "pool", "spa" };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => r.Tags.Any(t => tags.Contains(t)));
        translator.Translate(true);

        Assert.Equal("WHERE JSON_MATCH_ANY(MATCH_PARAM_JSON() IN (TO_JSON('pool'), TO_JSON('spa')), `Tags`)", translator.Clause.ToString());
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
    public void Translate_DateTime_UsesSingleStoreFormat()
    {
        var createdAt = new DateTime(2024, 6, 15, 13, 4, 5, 123);
        var dates = new[] { createdAt };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => dates.Contains(r.CreatedAt));
        translator.Translate(true);

        Assert.Equal(
            $"WHERE `CreatedAt` IN ('{createdAt.ToString("yyyy-MM-dd HH:mm:ss.ffffff")}')",
            translator.Clause.ToString());
    }

    [Fact]
    public void Translate_DateTimeOffsetUtc_UsesSingleStoreFormat()
    {
        var updatedAt = new DateTimeOffset(2024, 6, 15, 13, 4, 5, TimeSpan.Zero);
        var dates = new[] { updatedAt };
        var translator = new SingleStoreFilterTranslator(_model, (SingleStoreHotel<string> r) => dates.Contains(r.UpdatedAt));
        translator.Translate(true);

        Assert.Equal(
            $"WHERE `UpdatedAt` IN ('{updatedAt.ToString("yyyy-MM-dd HH:mm:ss.ffffff")}')",
            translator.Clause.ToString());
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
}
