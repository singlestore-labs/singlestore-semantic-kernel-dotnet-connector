using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace SingleStore.SemanticKernel.UnitTests;

public class TsvWriterTests
{
    [Fact]
    public async Task WriteRecordsAsync_EmptyRecords_WritesNothing()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("name", typeof(string)));

        var output = await WriteAsync(model, []);

        Assert.Equal("", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_MultipleColumnsAndRecords_SeparatesWithTabAndNewline()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("name", typeof(string)),
            new VectorStoreDataProperty("code", typeof(int)));

        var output = await WriteAsync(model,
        [
            Row(("id", "a"), ("name", "one"), ("code", 1)),
            Row(("id", "b"), ("name", "two"), ("code", 2))
        ]);

        Assert.Equal("a\tone\t1\nb\ttwo\t2\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_NullValue_WritesNullMarker()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("name", typeof(string)));

        var output = await WriteAsync(model, [Row(("id", "a"), ("name", null))]);

        Assert.Equal("a\t\\N\n", output);
    }

    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public async Task WriteRecordsAsync_Bool_WritesOneOrZero(bool value, string expected)
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("flag", typeof(bool)));

        var output = await WriteAsync(model, [Row(("id", "a"), ("flag", value))]);

        Assert.Equal($"a\t{expected}\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_DateTime_WritesMicrosecondTimestamp()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("created", typeof(DateTime)));
        var created = new DateTime(2024, 3, 15, 8, 9, 10, 123).AddTicks(4567);

        var output = await WriteAsync(model, [Row(("id", "a"), ("created", created))]);

        Assert.Equal($"a\t{created.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_UtcDateTimeOffset_WritesMicrosecondTimestamp()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("updated", typeof(DateTimeOffset)));
        var updated = new DateTimeOffset(2024, 3, 15, 8, 9, 10, TimeSpan.Zero).AddTicks(1234567);

        var output = await WriteAsync(model, [Row(("id", "a"), ("updated", updated))]);

        Assert.Equal($"a\t{updated.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_NonUtcDateTimeOffset_Throws()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("updated", typeof(DateTimeOffset)));
        var updated = new DateTimeOffset(2024, 3, 15, 8, 9, 10, TimeSpan.FromHours(2));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            WriteAsync(model, [Row(("id", "a"), ("updated", updated))]));

        Assert.Contains("only offset 0 (UTC) is supported", exception.Message);
    }

#if NET
    [Fact]
    public async Task WriteRecordsAsync_DateOnly_WritesIsoDate()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("day", typeof(DateOnly)));

        var output = await WriteAsync(model, [Row(("id", "a"), ("day", new DateOnly(2024, 3, 15)))]);

        Assert.Equal("a\t2024-03-15\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_TimeOnly_WritesMicrosecondTime()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("time", typeof(TimeOnly)));
        var time = new TimeOnly(8, 9, 10, 123).Add(TimeSpan.FromTicks(4567));

        var output = await WriteAsync(model, [Row(("id", "a"), ("time", time))]);

        Assert.Equal($"a\t{time.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}\n", output);
    }
#endif

    [Fact]
    public async Task WriteRecordsAsync_StringArrayAndList_WritesJson()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("tags", typeof(string[])),
            new VectorStoreDataProperty("labels", typeof(List<string>)));
        string[] tags = ["x", "y"];
        List<string> labels = ["p", "q"];

        var output = await WriteAsync(model, [Row(("id", "a"), ("tags", tags), ("labels", labels))]);

        Assert.Equal($"a\t{JsonSerializer.Serialize(tags)}\t{JsonSerializer.Serialize(labels)}\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_SpecialCharacters_EscapesBackslashTabAndNewline()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("name", typeof(string)));

        var output = await WriteAsync(model, [Row(("id", "a"), ("name", "a\\b\tc\nd"))]);

        Assert.Equal("a\ta\\\\b\\\tc\\\nd\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_ByteArray_WritesHex()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("payload", typeof(byte[])));

        var output = await WriteAsync(model, [Row(("id", "a"), ("payload", new byte[] { 0x00, 0x01, 0xFF, 0x0A, 0x09, (byte)'\\' }))]);

        Assert.Equal("a\t0001FF0A095C\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_EmptyByteArray_WritesEmptyField()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreDataProperty("payload", typeof(byte[])));

        var output = await WriteAsync(model, [Row(("id", "a"), ("payload", Array.Empty<byte>()))]);

        Assert.Equal("a\t\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_NumericAndDateTime_UsesInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.DateTimeFormat.TimeSeparator = ".";
        CultureInfo.CurrentCulture = culture;
        try
        {
            var model = BuildModel(
                new VectorStoreKeyProperty("id", typeof(string)),
                new VectorStoreDataProperty("rating", typeof(float)),
                new VectorStoreDataProperty("score", typeof(double)),
                new VectorStoreDataProperty("price", typeof(decimal)),
                new VectorStoreDataProperty("created", typeof(DateTime)));
            var created = new DateTime(2024, 3, 15, 8, 9, 10, 123).AddTicks(4567);

            var output = await WriteAsync(model, [Row(
                ("id", "a"),
                ("rating", 1.5f),
                ("score", 2.25d),
                ("price", 3.5m),
                ("created", created))]);

            Assert.Equal(
                $"a\t1.5\t2.25\t3.5\t{created.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}\n",
                output);
            Assert.DoesNotContain(",", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task WriteRecordsAsync_Vector_WritesJsonArray()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreVectorProperty("embedding", typeof(ReadOnlyMemory<float>), 3));
        ReadOnlyMemory<float> embedding = new[] { 1.5f, 2f, 3f };

        var output = await WriteAsync(model, [Row(("id", "a"), ("embedding", embedding))]);

        Assert.Equal($"a\t{JsonSerializer.Serialize(embedding)}\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_Embedding_WritesUnderlyingVectorJson()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreVectorProperty("embedding", typeof(Embedding<float>), 3));
        var embedding = new Embedding<float>(new[] { 1f, 2f, 3f });

        var output = await WriteAsync(model, [Row(("id", "a"), ("embedding", embedding))]);

        Assert.Equal($"a\t{JsonSerializer.Serialize(embedding.Vector)}\n", output);
    }

    [Fact]
    public async Task WriteRecordsAsync_GeneratedEmbeddings_OverrideRecordVector()
    {
        var model = BuildModel(
            new VectorStoreKeyProperty("id", typeof(string)),
            new VectorStoreVectorProperty("embedding", typeof(ReadOnlyMemory<float>), 3));
        var generated = new Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>
        {
            [model.VectorProperties[0]] = [new Embedding<float>(new[] { 9f, 8f, 7f })]
        };

        var output = await WriteAsync(
            model,
            [Row(("id", "a"), ("embedding", new ReadOnlyMemory<float>([1f, 2f, 3f])))],
            generated);

        Assert.Equal($"a\t{JsonSerializer.Serialize(new[] { 9f, 8f, 7f })}\n", output);
    }

    private static CollectionModel BuildModel(params VectorStoreProperty[] properties)
    {
        var definition = new VectorStoreCollectionDefinition { Properties = properties };
        return new SingleStoreModelBuilder().BuildDynamic(definition, null);
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values)
    {
        var row = new Dictionary<string, object?>();
        foreach (var (key, value) in values)
        {
            row[key] = value;
        }

        return row;
    }

    private static async Task<string> WriteAsync(
        CollectionModel model,
        IEnumerable<Dictionary<string, object?>> records,
        Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>? generatedEmbeddings = null,
        CancellationToken cancellationToken = default)
    {
        var pipe = new Pipe();
        var writeTask = TsvWriter<Dictionary<string, object?>>.WriteRecordsAsync(
            pipe.Writer,
            model,
            records,
            generatedEmbeddings,
            cancellationToken);
        var readTask = ReadAllAsync(pipe.Reader);

        await writeTask.ConfigureAwait(false);
        return await readTask.ConfigureAwait(false);
    }

    private static async Task<string> ReadAllAsync(PipeReader reader)
    {
        using var stream = reader.AsStream();
        using var streamReader = new StreamReader(stream, new UTF8Encoding(false));
        return await streamReader.ReadToEndAsync().ConfigureAwait(false);
    }
}
