namespace HistoryMap.Api.Models;

/// <summary>Upsert place + optional summary (idempotent by Ref).</summary>
public sealed class UpsertPlaceRequest
{
    public string? Ref { get; set; }
    public string? Name { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PlaceType { get; set; }
    public bool? IsNhl { get; set; }
    public DateOnly? ListedDate { get; set; }
    public string? SummaryText { get; set; }
    public string? SourceHeading { get; set; }
    public decimal? Confidence { get; set; }
    public string? SourceUrl { get; set; }
    public string? ReviewNote { get; set; }
}

/// <summary>Upsert photo metadata (idempotent by Ref + BlobPath). Binary upload is separate (Phase 2).</summary>
public sealed class UpsertPhotoRequest
{
    public string? Ref { get; set; }
    public string? BlobPath { get; set; }
    public string? Caption { get; set; }
    public string? Source { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public int? SortOrder { get; set; }
}

public sealed class IngestBatchRequest
{
    public List<UpsertPlaceRequest>? Places { get; set; }
    public List<UpsertPhotoRequest>? Photos { get; set; }
}

public sealed record UpsertPlaceResult(string Ref, bool Created, bool SummaryUpdated);

public sealed record UpsertPhotoResult(string Ref, string BlobPath, bool Created);

public sealed record IngestBatchResult(
    IReadOnlyList<UpsertPlaceResult> Places,
    IReadOnlyList<UpsertPhotoResult> Photos,
    IReadOnlyList<string> Errors);
