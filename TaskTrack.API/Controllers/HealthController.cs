using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Data;

namespace TaskTrack.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController(TaskTrackDbContext database) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        bool ready;
        try
        {
            ready = await database.Database.CanConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ready = false;
        }
        var result = new HealthResponse(ready ? "healthy" : "unhealthy",
            ready ? "reachable" : "unavailable", DateTimeOffset.UtcNow);
        return StatusCode(ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, result);
    }
}

public sealed record HealthResponse(string Status, string Database, DateTimeOffset CheckedAt);
