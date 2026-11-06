namespace HistoryMap.Api.Models;

public class Photo
{
    public int PhotoId { get; set; }
    public string Ref { get; set; } = string.Empty;
    /// <summary>Relative path within the Blob container (not a full URL).</summary>
    public string BlobPath { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? Source { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Place? Place { get; set; }
}
