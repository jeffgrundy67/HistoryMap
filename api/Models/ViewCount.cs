namespace HistoryMap.Api.Models;

public class ViewCount
{
    public string Ref { get; set; } = string.Empty;
    public DateOnly ViewDay { get; set; }
    public string AppName { get; set; } = string.Empty;
    public int ViewTotal { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Place? Place { get; set; }
}
