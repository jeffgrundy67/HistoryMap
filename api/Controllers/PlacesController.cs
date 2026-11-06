using HistoryMap.Api.Models;
using HistoryMap.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HistoryMap.Api.Controllers;

[ApiController]
[Route("api/v1/places")]
public class PlacesController : ControllerBase
{
    private readonly IPlacesService _places;
    private readonly ILogger<PlacesController> _logger;

    public PlacesController(IPlacesService places, ILogger<PlacesController> logger)
    {
        _places = places;
        _logger = logger;
    }

    /// <summary>
    /// Pins for the map viewport.
    /// bbox = minLng,minLat,maxLng,maxLat (comma-separated, WGS84).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlacePinDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlacePinDto>>> GetPlaces(
        [FromQuery] string bbox,
        [FromQuery] int? zoom,
        CancellationToken cancellationToken)
    {
        if (!TryParseBbox(bbox, out var minLng, out var minLat, out var maxLng, out var maxLat, out var error))
            return BadRequest(new { error });

        var pins = await _places.GetPlacesInBboxAsync(minLng, minLat, maxLng, maxLat, zoom, cancellationToken);
        return Ok(pins);
    }

    /// <summary>
    /// Refs that have at least one photo (for the "only places with photos" toggle).
    /// Declared before {ref} so routing does not treat "with-photos" as a ref.
    /// </summary>
    [HttpGet("with-photos")]
    [ProducesResponseType(typeof(PlacesWithPhotosDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlacesWithPhotosDto>> GetPlacesWithPhotos(CancellationToken cancellationToken)
    {
        var result = await _places.GetPlaceRefsWithPhotosAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Place detail with summary and photo URLs (Blob base + path).</summary>
    [HttpGet("{ref}")]
    [ProducesResponseType(typeof(PlaceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaceDetailDto>> GetPlaceByRef(
        [FromRoute(Name = "ref")] string placeRef,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(placeRef) || placeRef.Length > 12)
            return BadRequest(new { error = "ref must be 1–12 characters." });

        var place = await _places.GetPlaceByRefAsync(placeRef.Trim(), cancellationToken);
        if (place is null)
        {
            _logger.LogInformation("Place {Ref} not found", placeRef);
            return NotFound(new { error = "Place not found.", @ref = placeRef });
        }

        return Ok(place);
    }

    public static bool TryParseBbox(
        string? bbox,
        out decimal minLng,
        out decimal minLat,
        out decimal maxLng,
        out decimal maxLat,
        out string error)
    {
        minLng = minLat = maxLng = maxLat = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(bbox))
        {
            error = "bbox is required (minLng,minLat,maxLng,maxLat).";
            return false;
        }

        var parts = bbox.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !decimal.TryParse(parts[0], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out minLng)
            || !decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out minLat)
            || !decimal.TryParse(parts[2], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out maxLng)
            || !decimal.TryParse(parts[3], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out maxLat))
        {
            error = "bbox must be four comma-separated numbers: minLng,minLat,maxLng,maxLat.";
            return false;
        }

        if (minLng > maxLng || minLat > maxLat)
        {
            error = "bbox min values must be less than or equal to max values.";
            return false;
        }

        if (minLng < -180 || maxLng > 180 || minLat < -90 || maxLat > 90)
        {
            error = "bbox coordinates out of WGS84 range.";
            return false;
        }

        return true;
    }
}
