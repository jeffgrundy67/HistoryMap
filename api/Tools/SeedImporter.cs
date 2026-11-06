using System.Text.Json;
using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HistoryMap.Api.Tools;

/// <summary>
/// Stub importer for summaries.json and a photo inventory JSON once Azure SQL credentials exist.
/// Run: dotnet run --project api -- --seed [--summaries path] [--photos path]
/// </summary>
public static class SeedImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HistoryMapDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedImporter");

        var summariesPath = GetArg(args, "--summaries")
            ?? Path.Combine("..", "database", "seed", "summaries.json");
        var photosPath = GetArg(args, "--photos")
            ?? Path.Combine("..", "database", "seed", "photo-inventory.json");

        logger.LogInformation("Seed importer starting. Summaries={Summaries} Photos={Photos}", summariesPath, photosPath);

        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                logger.LogError("Cannot connect to Azure SQL. Set ConnectionStrings:HistoryMap and retry.");
                return 2;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database connection failed. Apply migrations and set secrets first.");
            return 2;
        }

        var summaryCount = 0;
        var photoCount = 0;

        if (File.Exists(summariesPath))
        {
            summaryCount = await ImportSummariesAsync(db, summariesPath, logger, cancellationToken);
        }
        else
        {
            logger.LogWarning("Summaries file not found at {Path}. Skipping. Place a summaries.json (array of SeedSummaryRow) and re-run.", summariesPath);
            WriteSampleIfMissing(summariesPath, SampleSummariesJson, logger);
        }

        if (File.Exists(photosPath))
        {
            photoCount = await ImportPhotosAsync(db, photosPath, logger, cancellationToken);
        }
        else
        {
            logger.LogWarning("Photo inventory not found at {Path}. Skipping. Place photo-inventory.json and re-run.", photosPath);
            WriteSampleIfMissing(photosPath, SamplePhotosJson, logger);
        }

        logger.LogInformation("Seed complete. Upserted summaries={Summaries}, photos={Photos}", summaryCount, photoCount);
        return 0;
    }

    private static async Task<int> ImportSummariesAsync(
        HistoryMapDbContext db,
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var rows = await JsonSerializer.DeserializeAsync<List<SeedSummaryRow>>(stream, JsonOptions, cancellationToken)
                   ?? new List<SeedSummaryRow>();

        var upserted = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Ref))
                continue;

            var place = await db.Places.FirstOrDefaultAsync(p => p.Ref == row.Ref, cancellationToken);
            if (place is null)
            {
                place = new Place
                {
                    Ref = row.Ref.Trim(),
                    Name = string.IsNullOrWhiteSpace(row.Name) ? row.Ref : row.Name!,
                    Latitude = row.Latitude ?? 0m,
                    Longitude = row.Longitude ?? 0m,
                    City = row.City,
                    State = row.State,
                    PlaceType = row.PlaceType,
                    IsNhl = row.IsNhl ?? false,
                    ListedDate = row.ListedDate,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.Places.Add(place);
            }
            else if (!string.IsNullOrWhiteSpace(row.Name))
            {
                place.Name = row.Name!;
                place.UpdatedAtUtc = DateTime.UtcNow;
                if (row.Latitude.HasValue) place.Latitude = row.Latitude.Value;
                if (row.Longitude.HasValue) place.Longitude = row.Longitude.Value;
            }

            if (!string.IsNullOrWhiteSpace(row.SummaryText))
            {
                var summary = await db.Summaries.FirstOrDefaultAsync(s => s.Ref == row.Ref, cancellationToken);
                if (summary is null)
                {
                    db.Summaries.Add(new Summary
                    {
                        Ref = row.Ref.Trim(),
                        SummaryText = row.SummaryText!,
                        SourceHeading = row.SourceHeading,
                        Confidence = row.Confidence,
                        SourceUrl = row.SourceUrl,
                        ReviewNote = row.ReviewNote,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    summary.SummaryText = row.SummaryText!;
                    summary.SourceHeading = row.SourceHeading;
                    summary.Confidence = row.Confidence;
                    summary.SourceUrl = row.SourceUrl;
                    summary.ReviewNote = row.ReviewNote;
                    summary.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

            upserted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Processed {Count} summary/place rows from {Path}", upserted, path);
        return upserted;
    }

    private static async Task<int> ImportPhotosAsync(
        HistoryMapDbContext db,
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var rows = await JsonSerializer.DeserializeAsync<List<SeedPhotoRow>>(stream, JsonOptions, cancellationToken)
                   ?? new List<SeedPhotoRow>();

        var upserted = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Ref) || string.IsNullOrWhiteSpace(row.BlobPath))
                continue;

            var placeExists = await db.Places.AnyAsync(p => p.Ref == row.Ref, cancellationToken);
            if (!placeExists)
            {
                logger.LogWarning("Skipping photo for missing place Ref={Ref}", row.Ref);
                continue;
            }

            var existing = await db.Photos.FirstOrDefaultAsync(
                p => p.Ref == row.Ref && p.BlobPath == row.BlobPath,
                cancellationToken);

            if (existing is null)
            {
                db.Photos.Add(new Photo
                {
                    Ref = row.Ref.Trim(),
                    BlobPath = row.BlobPath.Trim().TrimStart('/'),
                    Caption = row.Caption,
                    Source = row.Source,
                    WidthPx = row.WidthPx,
                    HeightPx = row.HeightPx,
                    SortOrder = row.SortOrder ?? 0,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.Caption = row.Caption;
                existing.Source = row.Source;
                existing.WidthPx = row.WidthPx;
                existing.HeightPx = row.HeightPx;
                if (row.SortOrder.HasValue) existing.SortOrder = row.SortOrder.Value;
            }

            upserted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Processed {Count} photo inventory rows from {Path}", upserted, path);
        return upserted;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static void WriteSampleIfMissing(string path, string content, ILogger logger)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var samplePath = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? path.Replace(".json", ".sample.json", StringComparison.OrdinalIgnoreCase)
                : path + ".sample.json";

            if (!File.Exists(samplePath))
            {
                File.WriteAllText(samplePath, content);
                logger.LogInformation("Wrote sample file at {Path}", samplePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not write sample file near {Path}", path);
        }
    }

    private const string SampleSummariesJson = """
[
  {
    "ref": "SAMPLE001",
    "name": "Sample Historic Place",
    "latitude": 38.907200,
    "longitude": -77.036900,
    "city": "Washington",
    "state": "DC",
    "placeType": "building",
    "isNhl": false,
    "summaryText": "Placeholder summary for local seed testing.",
    "sourceHeading": "Sample",
    "confidence": 0.500,
    "sourceUrl": null,
    "reviewNote": "Replace with real summaries.json before production import."
  }
]
""";

    private const string SamplePhotosJson = """
[
  {
    "ref": "SAMPLE001",
    "blobPath": "places/SAMPLE001/main.jpg",
    "caption": "Sample photo",
    "source": "inventory",
    "widthPx": 1200,
    "heightPx": 800,
    "sortOrder": 0
  }
]
""";

    public sealed class SeedSummaryRow
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

    public sealed class SeedPhotoRow
    {
        public string? Ref { get; set; }
        public string? BlobPath { get; set; }
        public string? Caption { get; set; }
        public string? Source { get; set; }
        public int? WidthPx { get; set; }
        public int? HeightPx { get; set; }
        public int? SortOrder { get; set; }
    }
}
