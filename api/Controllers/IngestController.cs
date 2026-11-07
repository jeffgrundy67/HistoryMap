using HistoryMap.Api.Auth;
using HistoryMap.Api.Models;
using HistoryMap.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HistoryMap.Api.Controllers;

/// <summary>
/// Authenticated write/ingest APIs for Grok Bot (and similar clients).
/// Public map GETs stay on PlacesController without [Authorize].
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.IngestWrite)]
[Route("api/v1/ingest")]
public class IngestController : ControllerBase
{
    private readonly IIngestService _ingest;
    private readonly ILogger<IngestController> _logger;

    public IngestController(IIngestService ingest, ILogger<IngestController> logger)
    {
        _ingest = ingest;
        _logger = logger;
    }

    /// <summary>Upsert place + optional summary by ref (idempotent).</summary>
    [HttpPut("places/{ref}")]
    [HttpPost("places/{ref}")]
    [ProducesResponseType(typeof(UpsertPlaceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UpsertPlaceResult>> UpsertPlaceByRef(
        [FromRoute(Name = "ref")] string placeRef,
        [FromBody] UpsertPlaceRequest request,
        CancellationToken cancellationToken)
    {
        request.Ref = placeRef;
        return await UpsertPlaceAsync(request, cancellationToken);
    }

    /// <summary>Upsert place + optional summary (ref in body).</summary>
    [HttpPut("places")]
    [HttpPost("places")]
    [ProducesResponseType(typeof(UpsertPlaceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UpsertPlaceResult>> UpsertPlace(
        [FromBody] UpsertPlaceRequest request,
        CancellationToken cancellationToken)
    {
        return await UpsertPlaceAsync(request, cancellationToken);
    }

    /// <summary>
    /// Upsert photo metadata (Ref + BlobPath). Upload the binary to Blob Storage separately (Phase 2 may add authenticated upload).
    /// </summary>
    [HttpPut("photos")]
    [HttpPost("photos")]
    [ProducesResponseType(typeof(UpsertPhotoResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpsertPhotoResult>> UpsertPhoto(
        [FromBody] UpsertPhotoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ingest.UpsertPhotoAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation("Photo upsert rejected: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Batch upsert places/summaries and photo metadata in one request.</summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(IngestBatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IngestBatchResult>> UpsertBatch(
        [FromBody] IngestBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ingest.UpsertBatchAsync(request ?? new IngestBatchRequest(), cancellationToken);
        return Ok(result);
    }

    private async Task<ActionResult<UpsertPlaceResult>> UpsertPlaceAsync(
        UpsertPlaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ingest.UpsertPlaceAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
