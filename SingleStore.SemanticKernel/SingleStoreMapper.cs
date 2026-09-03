using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace SingleStore.SemanticKernel;

/// <summary>
/// A mapper class that handles the conversion between data models and storage models for SingleStore vector store.
/// </summary>
/// <typeparam name="TRecord">The type of the data model record.</typeparam>
internal sealed class SingleStoreMapper<TRecord>(CollectionModel model)
    where TRecord : class
{
    public TRecord MapFromStorageToDataModel(DbDataReader reader, bool includeVectors)
    {
        var record = model.CreateRecord<TRecord>()!;

        PopulateValue(reader, model.KeyProperty, record);

        foreach (var property in model.DataProperties)
        {
            PopulateValue(reader, property, record);
        }

        if (includeVectors)
        {
            foreach (var property in model.VectorProperties)
            {
                PopulateValue(reader, property, record);
            }
        }

        return record;

        static void PopulateValue(DbDataReader reader, PropertyModel property, object record)
        {
            try
            {
                var ordinal = reader.GetOrdinal(property.StorageName);

                if (reader.IsDBNull(ordinal))
                {
                    property.SetValueAsObject(record, null);
                    return;
                }

                switch (Nullable.GetUnderlyingType(property.Type) ?? property.Type)
                {
                    case var t when t == typeof(bool):
                        property.SetValue(record, reader.GetBoolean(ordinal)); // TINYINT
                        break;
                    case var t when t == typeof(byte):
                        property.SetValue(record, reader.GetByte(ordinal)); // TINYINT UNSIGNED
                        break;
                    case var t when t == typeof(sbyte):
                        property.SetValue(record, reader.GetFieldValue<sbyte>(ordinal)); // TINYINT
                        break;
                    case var t when t == typeof(short):
                        property.SetValue(record, reader.GetInt16(ordinal)); // SMALLINT
                        break;
                    case var t when t == typeof(ushort):
                        property.SetValue(record, reader.GetFieldValue<ushort>(ordinal)); // SMALLINT UNSIGNED
                        break;
                    case var t when t == typeof(int):
                        property.SetValue(record, reader.GetInt32(ordinal)); // INT
                        break;
                    case var t when t == typeof(uint):
                        property.SetValue(record, reader.GetFieldValue<uint>(ordinal)); // INT UNSIGNED
                        break;
                    case var t when t == typeof(long):
                        property.SetValue(record, reader.GetInt64(ordinal)); // BIGINT
                        break;
                    case var t when t == typeof(ulong):
                        property.SetValue(record, reader.GetFieldValue<ulong>(ordinal)); // BIGINT UNSIGNED
                        break;

                    case var t when t == typeof(float):
                        property.SetValue(record, reader.GetFloat(ordinal)); // FLOAT
                        break;
                    case var t when t == typeof(double):
                        property.SetValue(record, reader.GetDouble(ordinal)); // DOUBLE
                        break;
                    case var t when t == typeof(decimal):
                        property.SetValue(record, reader.GetDecimal(ordinal)); // DECIMAL
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(record, reader.GetString(ordinal)); // LONGTEXT
                        break;
                    case var t when t == typeof(byte[]):
                        property.SetValueAsObject(record, reader.GetValue(ordinal)); // LONGBLOB
                        break;
                    case var t when t == typeof(DateTime):
                        property.SetValue(record, reader.GetDateTime(ordinal)); // DATETIME(6)
                        break;
                    case var t when t == typeof(DateTimeOffset):
                        property.SetValue(record,
                            new DateTimeOffset(
                                DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc))); // DATETIME(6)
                        break;
#if NET
                    case var t when t == typeof(DateOnly):
                        property.SetValue(record, reader.GetFieldValue<DateOnly>(ordinal)); // DATE
                        break;
                    case var t when t == typeof(TimeOnly):
                        property.SetValue(record, reader.GetFieldValue<TimeOnly>(ordinal)); // TIME(6)
                        break;
#endif
                    case var t when t == typeof(Guid):
                        property.SetValue(record, reader.GetGuid(ordinal)); // UNIQUEIDENTIFIER
                        break;

                    // We map string[] and List<string> properties to SingleStore JSON columns, so deserialize from JSON here.
                    case var t when t == typeof(string[]):
                        property.SetValue(record,
                            JsonSerializer.Deserialize<string[]>(
                                reader.GetString(ordinal)));
                        break;
                    case var t when t == typeof(List<string>):
                        property.SetValue(record,
                            JsonSerializer.Deserialize<List<string>>(
                                reader.GetString(ordinal)));
                        break;
                    case var t when t == typeof(ReadOnlyMemory<float>):
                        property.SetValueAsObject(record,
                            reader.GetFieldValue<ReadOnlyMemory<float>>(ordinal));
                        break;
                    case var t when t == typeof(Embedding<float>):
                        property.SetValueAsObject(record,
                            new Embedding<float>(
                                reader.GetFieldValue<ReadOnlyMemory<float>>(ordinal)));
                        break;
                    case var t when t == typeof(float[]):
                        var vector = reader.GetFieldValue<ReadOnlyMemory<float>>(ordinal);
                        property.SetValueAsObject(record,
                            MemoryMarshal.TryGetArray(vector, out var segment)
                            && segment.Count == segment.Array!.Length
                                ? segment.Array
                                : vector.ToArray());
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported type '{property.Type.Name}' for property '{property.ModelName}'.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read property '{property.ModelName}' of type '{property.Type.Name}'.", ex);
            }
        }
    }
}
