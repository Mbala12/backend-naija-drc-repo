using Consular.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consular.Api.Controllers;

// Lets the frontend forward its own runtime errors (uncaught exceptions, unhandled promise
// rejections, React render errors — see the frontend's errorReporting.js/ErrorBoundary) so they
// land in the same backend logs as everything else, instead of being visible only in whichever
// user's browser console hit them. Anonymous: these can happen on public pages (home, login,
// tracking) before any auth token exists. Logged via the same plain ILogger everything else in
// this API uses — this endpoint only gives the frontend a way to reach it, it doesn't change
// how backend logging itself works.
[ApiController]
[Route("api/v1/logs")]
[AllowAnonymous]
public class LogsController : ControllerBase
{
    // Keeps a misbehaving or malicious client from writing unbounded log entries.
    private const int MaxFieldLength = 4000;

    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogger<LogsController> logger)
    {
        _logger = logger;
    }

    [HttpPost("client")]
    public IActionResult LogClientError(ClientErrorDto dto)
    {
        var message = Truncate(dto.Message);
        var url = Truncate(dto.Url);
        var userAgent = Truncate(dto.UserAgent);
        var stack = Truncate(dto.Stack);

        if (string.Equals(dto.Level, "warn", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Frontend warning: {Message} | url={Url} | userAgent={UserAgent} | stack={Stack}",
                message, url, userAgent, stack);
        }
        else
        {
            _logger.LogError("Frontend error: {Message} | url={Url} | userAgent={UserAgent} | stack={Stack}",
                message, url, userAgent, stack);
        }

        return NoContent();
    }

    private static string? Truncate(string? value) =>
        value is null ? null : (value.Length > MaxFieldLength ? value[..MaxFieldLength] : value);
}
