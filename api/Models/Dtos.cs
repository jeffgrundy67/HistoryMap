namespace HistoryMap.Api.Models;

public record PlacePinDto(
    string Ref,
    string Name,
    decimal Latitude,
    decimal Longitude,
    string? PlaceType,
    bool IsNhl,
    bool HasPhotos);

public record PlaceDetailDto(
    string Ref,
    string Name,
    decimal Latitude,
    decimal Longitude,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? PlaceType,
    bool IsNhl,
    DateOnly? ListedDate,
    SummaryDto? Summary,
    IReadOnlyList<PhotoDto> Photos);

public record SummaryDto(
    string SummaryText,
    string? SourceHeading,
    decimal? Confidence,
    string? SourceUrl,
    string? ReviewNote);

public record PhotoDto(
    int PhotoId,
    string Url,
    string? Caption,
    string? Source,
    int? WidthPx,
    int? HeightPx,
    int SortOrder);

public record PlacesWithPhotosDto(IReadOnlyList<string> Refs);

public record HealthResponse(
    string Status,
    string Service,
    DateTime Utc,
    string? Database);
