using System.Text.Json;
using HistoryMap.Api.Models;
using HistoryMap.Api.Services;

namespace HistoryMap.Api.Tools;

/// <summary>
/// CLI importer for summaries.json and photo inventory JSON.
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
        var ingest = scope.ServiceProvider.GetRequiredService<IIngestService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedImporter");
        var db = scope.ServiceProvider.GetRequiredService<Data.HistoryMapDbContext>();

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
            summaryCount = await ImportSummariesAsync(ingest, summariesPath, logger, cancellationToken);
        }
        else
        {
            logger.LogWarning("Summaries file not found at {Path}. Skipping. Place a summaries.json (array of UpsertPlaceRequest) and re-run.", summariesPath);
            WriteSampleIfMissing(summariesPath, SampleSummariesJson, logger);
        }

        if (File.Exists(photosPath))
        {
            photoCount = await ImportPhotosAsync(ingest, photosPath, logger, cancellationToken);
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
        IIngestService ingest,
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var rows = await JsonSerializer.DeserializeAsync<List<UpsertPlaceRequest>>(stream, JsonOptions, cancellationToken)
                   ?? new List<UpsertPlaceRequest>();

        var upserted = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Ref))
                continue;

            await ingest.UpsertPlaceAsync(row, cancellationToken);
            upserted++;
        }

        logger.LogInformation("Processed {Count} summary/place rows from {Path}", upserted, path);
        return upserted;
    }

    private static async Task<int> ImportPhotosAsync(
        IIngestService ingest,
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var rows = await JsonSerializer.DeserializeAsync<List<UpsertPhotoRequest>>(stream, JsonOptions, cancellationToken)
                   ?? new List<UpsertPhotoRequest>();

        var upserted = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Ref) || string.IsNullOrWhiteSpace(row.BlobPath))
                continue;

            try
            {
                await ingest.UpsertPhotoAsync(row, cancellationToken);
                upserted++;
            }
            catch (InvalidOperationException)
            {
                logger.LogWarning("Skipping photo for missing place Ref={Ref}", row.Ref);
            }
        }

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
}
