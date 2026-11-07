using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HistoryMap.Api.Services;

/// <summary>
/// Idempotent place/summary/photo upserts by Place Ref (and Ref+BlobPath for photos).
/// Shared by HTTP ingest APIs and the SeedImporter CLI.
/// </summary>
public class IngestService : IIngestService
{
    private readonly HistoryMapDbContext _db;
    private readonly ILogger<IngestService> _logger;

    public IngestService(HistoryMapDbContext db, ILogger<IngestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UpsertPlaceResult> UpsertPlaceAsync(
        UpsertPlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await UpsertPlaceCoreAsync(request, save: true, cancellationToken);
        return result;
    }

    public async Task<UpsertPhotoResult> UpsertPhotoAsync(
        UpsertPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UpsertPhotoCoreAsync(request, save: true, cancellationToken);
    }

    public async Task<IngestBatchResult> UpsertBatchAsync(
        IngestBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var places = new List<UpsertPlaceResult>();
        var photos = new List<UpsertPhotoResult>();
        var errors = new List<string>();

        foreach (var place in request.Places ?? [])
        {
            try
            {
                places.Add(await UpsertPlaceCoreAsync(place, save: false, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                errors.Add($"place:{place.Ref ?? "?"}: {ex.Message}");
            }
        }

        // Persist places before photo FK checks so subsequent AnyAsync sees new refs.
        if (places.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        foreach (var photo in request.Photos ?? [])
        {
            try
            {
                photos.Add(await UpsertPhotoCoreAsync(photo, save: false, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                errors.Add($"photo:{photo.Ref ?? "?"}/{photo.BlobPath ?? "?"}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"photo:{photo.Ref ?? "?"}/{photo.BlobPath ?? "?"}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Batch ingest saved places={Places} photos={Photos} errors={Errors}",
            places.Count,
            photos.Count,
            errors.Count);

        return new IngestBatchResult(places, photos, errors);
    }

    private async Task<UpsertPlaceResult> UpsertPlaceCoreAsync(
        UpsertPlaceRequest row,
        bool save,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.Ref))
            throw new ArgumentException("ref is required.");

        var placeRef = row.Ref.Trim();
        if (placeRef.Length > 12)
            throw new ArgumentException("ref must be 1–12 characters.");

        var place = await _db.Places.FirstOrDefaultAsync(p => p.Ref == placeRef, cancellationToken);
        var created = place is null;

        if (place is null)
        {
            place = new Place
            {
                Ref = placeRef,
                Name = string.IsNullOrWhiteSpace(row.Name) ? placeRef : row.Name!.Trim(),
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
            _db.Places.Add(place);
        }
        else
        {
            place.UpdatedAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(row.Name)) place.Name = row.Name.Trim();
            if (row.Latitude.HasValue) place.Latitude = row.Latitude.Value;
            if (row.Longitude.HasValue) place.Longitude = row.Longitude.Value;
            if (row.City is not null) place.City = row.City;
            if (row.State is not null) place.State = row.State;
            if (row.PlaceType is not null) place.PlaceType = row.PlaceType;
            if (row.IsNhl.HasValue) place.IsNhl = row.IsNhl.Value;
            if (row.ListedDate.HasValue) place.ListedDate = row.ListedDate;
        }

        var summaryUpdated = false;
        if (!string.IsNullOrWhiteSpace(row.SummaryText))
        {
            summaryUpdated = true;
            var summary = await _db.Summaries.FirstOrDefaultAsync(s => s.Ref == placeRef, cancellationToken);
            if (summary is null)
            {
                _db.Summaries.Add(new Summary
                {
                    Ref = placeRef,
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

        if (save)
            await _db.SaveChangesAsync(cancellationToken);

        return new UpsertPlaceResult(placeRef, created, summaryUpdated);
    }

    private async Task<UpsertPhotoResult> UpsertPhotoCoreAsync(
        UpsertPhotoRequest row,
        bool save,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.Ref))
            throw new ArgumentException("ref is required.");
        if (string.IsNullOrWhiteSpace(row.BlobPath))
            throw new ArgumentException("blobPath is required.");

        var placeRef = row.Ref.Trim();
        if (placeRef.Length > 12)
            throw new ArgumentException("ref must be 1–12 characters.");

        var blobPath = row.BlobPath.Trim().TrimStart('/');
        if (blobPath.Length > 512)
            throw new ArgumentException("blobPath is too long.");

        var placeExists = await _db.Places.AnyAsync(p => p.Ref == placeRef, cancellationToken);
        if (!placeExists)
            throw new InvalidOperationException($"Place Ref={placeRef} does not exist. Upsert the place first.");

        var existing = await _db.Photos.FirstOrDefaultAsync(
            p => p.Ref == placeRef && p.BlobPath == blobPath,
            cancellationToken);

        var created = existing is null;
        if (existing is null)
        {
            _db.Photos.Add(new Photo
            {
                Ref = placeRef,
                BlobPath = blobPath,
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

        if (save)
            await _db.SaveChangesAsync(cancellationToken);

        return new UpsertPhotoResult(placeRef, blobPath, created);
    }
}
