namespace HistoryMap.Api.Models;

public class SummaryQueueItem
{
    public string Ref { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int Priority { get; set; } = 100;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime EnqueuedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Place? Place { get; set; }
}
