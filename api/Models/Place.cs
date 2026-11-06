namespace HistoryMap.Api.Models;

public class Place
{
    public string Ref { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? PlaceType { get; set; }
    public bool IsNhl { get; set; }
    public DateOnly? ListedDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Summary? Summary { get; set; }
    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    public ICollection<ViewCount> ViewCounts { get; set; } = new List<ViewCount>();
    public SummaryQueueItem? SummaryQueueItem { get; set; }
}
