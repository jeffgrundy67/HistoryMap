using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HistoryMap.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class HealthController : ControllerBase
{
    private readonly HistoryMapDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(HistoryMapDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("health")]
    [HttpGet("/health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> GetHealth(CancellationToken cancellationToken)
    {
        string? dbStatus;
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            dbStatus = canConnect ? "up" : "down";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check could not reach the database");
            dbStatus = "error";
        }

        var healthy = dbStatus == "up";
        var body = new HealthResponse(
            healthy ? "healthy" : "degraded",
            "HistoryMap.Api",
            DateTime.UtcNow,
            dbStatus);

        return healthy ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
