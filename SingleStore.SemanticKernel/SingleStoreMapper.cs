using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;
using SingleStoreConnector;

namespace SingleStore.SemanticKernel;

/// <summary>
/// A mapper class that handles the conversion between data models and storage models for SingleStore vector store.
/// </summary>
/// <typeparam name="TRecord">The type of the data model record.</typeparam>
internal sealed class SingleStoreMapper<TRecord>(CollectionModel model)
    where TRecord : class
{
    public TRecord MapFromStorageToDataModel(SingleStoreDataReader reader, bool includeVectors)
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
                var elementType = Nullable.GetUnderlyingType(property.EmbeddingType) ?? property.EmbeddingType;
                switch (Nullable.GetUnderlyingType(elementType) ?? elementType)
                {
                    case var t when t == typeof(ReadOnlyMemory<sbyte>) || t == typeof(Embedding<sbyte>) || t == typeof(sbyte[]):
                        PopulateVectorValue<sbyte>(reader, property, record);
                        break;
                    case var t when t == typeof(ReadOnlyMemory<short>) || t == typeof(Embedding<short>) || t == typeof(short[]):
                        PopulateVectorValue<short>(reader, property, record);
                        break;
                    case var t when t == typeof(ReadOnlyMemory<int>) || t == typeof(Embedding<int>) || t == typeof(int[]):
                        PopulateVectorValue<int>(reader, property, record);
                        break;
                    case var t when t == typeof(ReadOnlyMemory<long>) || t == typeof(Embedding<long>) || t == typeof(long[]):
                        PopulateVectorValue<long>(reader, property, record);
                        break;
                    case var t when t == typeof(ReadOnlyMemory<float>) || t == typeof(Embedding<float>) || t == typeof(float[]):
                        PopulateVectorValue<float>(reader, property, record);
                        break;
                    case var t when t == typeof(ReadOnlyMemory<double>) || t == typeof(Embedding<double>) || t == typeof(double[]):
                        PopulateVectorValue<double>(reader, property, record);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported element type '{elementType.Name}' for vector property '{property.ModelName}'.");
                }
            }
        }

        return record;

        static void PopulateVectorValue<TElement>(SingleStoreDataReader reader, VectorPropertyModel property, object record)
        {
            try
            {
                var ordinal = reader.GetOrdinal(property.StorageName);

                if (!reader.IsDBNull(ordinal))
                {
                    var vector = reader.GetFieldValue<ReadOnlyMemory<TElement>>(ordinal);

                    property.SetValueAsObject(record,
                        property.Type switch
                        {
                            var t when t == typeof(ReadOnlyMemory<TElement>) => vector,
                            var t when t == typeof(Embedding<TElement>) => new Embedding<TElement>(vector),
                            var t when t == typeof(TElement[])
                                => MemoryMarshal.TryGetArray(vector, out var segment)
                                   && segment.Count == segment.Array!.Length
                                    ? segment.Array
                                    : vector.ToArray(),

                            _ => throw new NotSupportedException($"Unsupported type '{property.Type.Name}' for vector property '{property.ModelName}'.")
                        });
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to deserialize vector property '{property.ModelName}'.", e);
            }
        }

        static void PopulateValue(SingleStoreDataReader reader, PropertyModel property, object record)
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
                        property.SetValue(record, reader.GetSByte(ordinal)); // TINYINT
                        break;
                    case var t when t == typeof(short):
                        property.SetValue(record, reader.GetInt16(ordinal)); // SMALLINT
                        break;
                    case var t when t == typeof(ushort):
                        property.SetValue(record, reader.GetUInt16(ordinal)); // SMALLINT UNSIGNED
                        break;
                    case var t when t == typeof(int):
                        property.SetValue(record, reader.GetInt32(ordinal)); // INT
                        break;
                    case var t when t == typeof(uint):
                        property.SetValue(record, reader.GetUInt32(ordinal)); // INT UNSIGNED
                        break;
                    case var t when t == typeof(long):
                        property.SetValue(record, reader.GetInt64(ordinal)); // BIGINT
                        break;
                    case var t when t == typeof(ulong):
                        property.SetValue(record, reader.GetUInt64(ordinal)); // BIGINT UNSIGNED
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
                        property.SetValue(record, reader.GetDateTimeOffset(ordinal)); // DATETIME(6)
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
