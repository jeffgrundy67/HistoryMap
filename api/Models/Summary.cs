namespace HistoryMap.Api.Models;

public class Summary
{
    public string Ref { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public string? SourceHeading { get; set; }
    public decimal? Confidence { get; set; }
    public string? SourceUrl { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Place? Place { get; set; }
}
