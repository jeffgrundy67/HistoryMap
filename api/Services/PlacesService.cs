using HistoryMap.Api.Configuration;
using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HistoryMap.Api.Services;

public class PlacesService : IPlacesService
{
    private readonly HistoryMapDbContext _db;
    private readonly BlobStorageOptions _blob;

    public PlacesService(HistoryMapDbContext db, IOptions<BlobStorageOptions> blobOptions)
    {
        _db = db;
        _blob = blobOptions.Value;
    }

    public async Task<IReadOnlyList<PlacePinDto>> GetPlacesInBboxAsync(
        decimal minLng,
        decimal minLat,
        decimal maxLng,
        decimal maxLat,
        int? zoom,
        CancellationToken cancellationToken = default)
    {
        // Zoom can later drive clustering / density caps; for v1 we only filter bbox.
        _ = zoom;

        var query =
            from p in _db.Places.AsNoTracking()
            where p.Longitude >= minLng
                  && p.Longitude <= maxLng
                  && p.Latitude >= minLat
                  && p.Latitude <= maxLat
            select new PlacePinDto(
                p.Ref,
                p.Name,
                p.Latitude,
                p.Longitude,
                p.PlaceType,
                p.IsNhl,
                p.Photos.Any());

        return await query
            .OrderBy(p => p.Name)
            .Take(2000)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlaceDetailDto?> GetPlaceByRefAsync(
        string placeRef,
        CancellationToken cancellationToken = default)
    {
        var place = await _db.Places
            .AsNoTracking()
            .Include(p => p.Summary)
            .Include(p => p.Photos.OrderBy(ph => ph.SortOrder).ThenBy(ph => ph.PhotoId))
            .FirstOrDefaultAsync(p => p.Ref == placeRef, cancellationToken);

        if (place is null)
            return null;

        SummaryDto? summary = place.Summary is null
            ? null
            : new SummaryDto(
                place.Summary.SummaryText,
                place.Summary.SourceHeading,
                place.Summary.Confidence,
                place.Summary.SourceUrl,
                place.Summary.ReviewNote);

        var photos = place.Photos
            .Select(ph => new PhotoDto(
                ph.PhotoId,
                _blob.BuildUrl(ph.BlobPath),
                ph.Caption,
                ph.Source,
                ph.WidthPx,
                ph.HeightPx,
                ph.SortOrder))
            .ToList();

        return new PlaceDetailDto(
            place.Ref,
            place.Name,
            place.Latitude,
            place.Longitude,
            place.AddressLine1,
            place.AddressLine2,
            place.City,
            place.State,
            place.PostalCode,
            place.Country,
            place.PlaceType,
            place.IsNhl,
            place.ListedDate,
            summary,
            photos);
    }

    public async Task<PlacesWithPhotosDto> GetPlaceRefsWithPhotosAsync(
        CancellationToken cancellationToken = default)
    {
        var refs = await _db.Photos
            .AsNoTracking()
            .Select(p => p.Ref)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync(cancellationToken);

        return new PlacesWithPhotosDto(refs);
    }
}
