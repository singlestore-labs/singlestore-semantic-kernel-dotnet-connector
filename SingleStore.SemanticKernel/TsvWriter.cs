using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace SingleStore.SemanticKernel;

internal static class TsvWriter<TRecord>
    where TRecord : class
{
    internal static async Task WriteRecordsAsync(PipeWriter pipeWriter,
        CollectionModel model,
        IEnumerable<TRecord> records,
        Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>? generatedEmbeddings,
        CancellationToken cancellationToken)
    {
        // TODO: handle key generations
        Exception? error = null;
        try
        {
            using var stream = pipeWriter.AsStream(true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
            foreach (var record in records)
            {
                for (var i = 0; i < model.Properties.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var value = model.Properties[i].GetValueAsObject(record);
                    if (model.Properties[i] is VectorPropertyModel vectorProperty)
                    {
                        if (generatedEmbeddings != null && generatedEmbeddings.ContainsKey(vectorProperty))
                        {
                            await writer.WriteAsync(EscapeTsv(JsonSerializer.Serialize(generatedEmbeddings[vectorProperty]))).ConfigureAwait(false);
                        }
                        else if (value is null)
                        {
                            await writer.WriteAsync("\\N").ConfigureAwait(false);
                        }
                        else
                        {
                            await writer.WriteAsync(EscapeTsv(JsonSerializer.Serialize(value))).ConfigureAwait(false);
                        }
                    }
                    else if (value is null)
                    {
                        await writer.WriteAsync("\\N").ConfigureAwait(false);
                    }
                    else if (value is bool boolValue)
                    {
                        await writer.WriteAsync(boolValue ? "1" : "0").ConfigureAwait(false);
                    }
                    else if (value is DateTime dateTimeValue)
                    {
                        await writer.WriteAsync(EscapeTsv(dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss.ffffff"))).ConfigureAwait(false);
                    }
                    else if (value is DateTimeOffset dateTimeOffsetValue)
                    {
                        if (dateTimeOffsetValue.Offset != TimeSpan.Zero)
                        {
                            throw new ArgumentException(
                                $"Cannot write DateTimeOffset with Offset={dateTimeOffsetValue.Offset}, only offset 0 (UTC) is supported.",
                                nameof(value));
                        }

                        await writer.WriteAsync(EscapeTsv(dateTimeOffsetValue.ToString("yyyy-MM-dd HH:mm:ss.ffffff"))).ConfigureAwait(false);
                    }
#if NET
                    else if (value is DateOnly dateOnlyValue)
                    {
                        await writer.WriteAsync(EscapeTsv(dateOnlyValue.ToString("yyyy-MM-dd"))).ConfigureAwait(false);
                    }
                    else if (value is TimeOnly timeOnly)
                    {
                        await writer.WriteAsync(EscapeTsv(timeOnly.ToString("HH:mm:ss.ffffff"))).ConfigureAwait(false);
                    }
#endif
                    else if (value is string[] stringArrayValue)
                    {
                        await writer.WriteAsync(EscapeTsv(JsonSerializer.Serialize(stringArrayValue))).ConfigureAwait(false);
                    }
                    else if (value is List<string> stringListValue)
                    {
                        await writer.WriteAsync(EscapeTsv(JsonSerializer.Serialize(stringListValue))).ConfigureAwait(false);
                    }
                    else
                    {
                        await writer.WriteAsync(EscapeTsv(value.ToString())).ConfigureAwait(false);
                    }

                    var token = i + 1 == model.Properties.Count ? "\n" : "\t";
                    await writer.WriteAsync(token).ConfigureAwait(false);
                }
            }

            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            // This is EOF. LoadAsync waits for it.
            await pipeWriter.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    private static string EscapeTsv(string data)
    {
        return data.Replace("\\", "\\\\").Replace("\n", "\\\n").Replace("\t", "\\\t");
    }
}
