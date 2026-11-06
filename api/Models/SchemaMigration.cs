namespace HistoryMap.Api.Models;

public class SchemaMigration
{
    public string MigrationId { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public string? Notes { get; set; }
}
