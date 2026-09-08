using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace SingleStore.SemanticKernel;

internal static class TsvWriter<TRecord>
    where TRecord : class
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.ffffff";
#if NET
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm:ss.ffffff";
#endif

    internal static async Task WriteRecordsAsync(PipeWriter pipeWriter,
        CollectionModel model,
        IEnumerable<TRecord> records,
        Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>? generatedEmbeddings,
        CancellationToken cancellationToken)
    {
        Exception? error = null;
        try
        {
#if NET
            await using var stream = pipeWriter.AsStream(true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
#else
            using var stream = pipeWriter.AsStream(true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
#endif
            var recordIndex = 0;
            foreach (var record in records)
            {
                for (var i = 0; i < model.Properties.Count; i++)
                {
                    var value = model.Properties[i].GetValueAsObject(record);
                    if (model.Properties[i] is VectorPropertyModel vectorProperty && generatedEmbeddings?.TryGetValue(vectorProperty, out var ge) is true)
                    {
                        value = ge[recordIndex];
                    }

                    var escapedValue = value switch
                    {
                        Embedding<float> e => EscapeTsv(JsonSerializer.Serialize(e.Vector)),
                        bool boolValue => boolValue ? "1" : "0",
                        DateTime dateTimeValue => dateTimeValue.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                        DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue.Offset == TimeSpan.Zero
                            ? dateTimeOffsetValue.ToString(DateTimeFormat, CultureInfo.InvariantCulture)
                            : throw new ArgumentException($"Cannot write DateTimeOffset with Offset={dateTimeOffsetValue.Offset}, only offset 0 (UTC) is supported.", nameof(value)),
#if NET
                        DateOnly dateOnlyValue => dateOnlyValue.ToString(DateFormat, CultureInfo.InvariantCulture),
                        TimeOnly timeOnlyValue => timeOnlyValue.ToString(TimeFormat, CultureInfo.InvariantCulture),
#endif
                        string[] stringArrayValue => EscapeTsv(JsonSerializer.Serialize(stringArrayValue)),
                        List<string> stringListValue => EscapeTsv(JsonSerializer.Serialize(stringListValue)),
                        byte[] bytes => ToHex(bytes),
                        null => "\\N",
                        _ => EscapeTsv(model.Properties[i] is VectorPropertyModel ? JsonSerializer.Serialize(value) : FormatInvariant(value))
                    };
                    await WriteAsync(writer, escapedValue, cancellationToken).ConfigureAwait(false);

                    var token = i + 1 == model.Properties.Count ? "\n" : "\t";
                    await WriteAsync(writer, token, cancellationToken).ConfigureAwait(false);
                }

                recordIndex++;
            }

            await FlushAsync(writer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            await pipeWriter.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    private static Task WriteAsync(StreamWriter writer, string value, CancellationToken cancellationToken)
    {
#if NET
        return writer.WriteAsync(value.AsMemory(), cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return writer.WriteAsync(value);
#endif
    }

    private static Task FlushAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
#if NET
        return writer.FlushAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return writer.FlushAsync();
#endif
    }

    private static string FormatInvariant(object value)
        => value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)!
            : value.ToString()!;

    private static string ToHex(byte[] bytes)
    {
#if NET
        return Convert.ToHexString(bytes);
#else
        return BitConverter.ToString(bytes).Replace("-", "");
#endif
    }

    private static string EscapeTsv(string data)
    {
        return data.Replace("\\", "\\\\").Replace("\n", "\\\n").Replace("\t", "\\\t");
    }
}
