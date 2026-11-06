using HistoryMap.Api.Models;

namespace HistoryMap.Api.Services;

public interface IPlacesService
{
    Task<IReadOnlyList<PlacePinDto>> GetPlacesInBboxAsync(
        decimal minLng,
        decimal minLat,
        decimal maxLng,
        decimal maxLat,
        int? zoom,
        CancellationToken cancellationToken = default);

    Task<PlaceDetailDto?> GetPlaceByRefAsync(
        string placeRef,
        CancellationToken cancellationToken = default);

    Task<PlacesWithPhotosDto> GetPlaceRefsWithPhotosAsync(
        CancellationToken cancellationToken = default);
}
