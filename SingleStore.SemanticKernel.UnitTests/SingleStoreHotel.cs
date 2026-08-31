using System;
using System.Collections.Generic;
using Microsoft.Extensions.VectorData;

namespace SingleStore.SemanticKernel.UnitTests;

/// <summary>
/// A test model for the singlestore vector store.
/// </summary>
public record SingleStoreHotel<T>
{
    /// <summary>The key of the record.</summary>
    [VectorStoreKey]
    public T HotelId { get; init; }

    /// <summary>A string metadata field.</summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string? HotelName { get; set; }

    /// <summary>An int metadata field.</summary>
    [VectorStoreData(IsIndexed = true)]
    public int HotelCode { get; set; }

    /// <summary>A float metadata field.</summary>
    [VectorStoreData]
    public float? HotelRating { get; set; }

    /// <summary>A bool metadata field.</summary>
    [VectorStoreData(StorageName = "parking_is_included")]
    public bool ParkingIncluded { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public List<string> Tags { get; set; } = [];

    /// <summary>A data field.</summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; }

    [VectorStoreData]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [VectorStoreData]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>A vector field.</summary>
    [VectorStoreVector(4, DistanceFunction = DistanceFunction.EuclideanDistance, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}
