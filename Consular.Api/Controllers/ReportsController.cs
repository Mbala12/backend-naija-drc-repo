using System.Security.Claims;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consular.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = "Permission.Reports.View")]
public class ReportsController : ControllerBase
{
    private readonly ReportAggregationService _reports;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ReportAggregationService reports, ILogger<ReportsController> logger)
    {
        _reports = reports;
        _logger = logger;
    }

    private string CurrentActor => User.FindFirstValue("displayName") ?? User.Identity?.Name ?? "staff";

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> GetSummary(DateOnly from, DateOnly to, string? equipe, CancellationToken ct)
    {
        if (to < from)
        {
            ModelState.AddModelError(nameof(to), "La date de fin doit être postérieure ou égale à la date de début.");
            return ValidationProblem(ModelState);
        }

        var summary = await _reports.BuildSummaryAsync(from, to, equipe, ct);
        _logger.LogDebug("{Actor} generated a report summary ({From} to {To}, equipe={Equipe})", CurrentActor, from, to, equipe ?? "*");
        return Ok(summary);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(DateOnly from, DateOnly to, string format, string? equipe, CancellationToken ct)
    {
        if (to < from)
        {
            ModelState.AddModelError(nameof(to), "La date de fin doit être postérieure ou égale à la date de début.");
            return ValidationProblem(ModelState);
        }

        var rows = await _reports.BuildExportRowsAsync(from, to, equipe, ct);
        _logger.LogInformation("{Actor} exported {Count} demande(s) as {Format} ({From} to {To}, equipe={Equipe})",
            CurrentActor, rows.Count, format, from, to, equipe ?? "*");

        var fileNameStem = $"rapport_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}";

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = ReportExportWriter.WriteExcel(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileNameStem}.xlsx");
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = ReportExportWriter.WriteCsv(rows);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{fileNameStem}.csv");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = ReportExportWriter.WritePdf(rows, $"Rapport {from:yyyy-MM-dd} — {to:yyyy-MM-dd}");
            return File(bytes, "application/pdf", $"{fileNameStem}.pdf");
        }

        ModelState.AddModelError(nameof(format), "Le format doit être 'csv', 'xlsx' ou 'pdf'.");
        return ValidationProblem(ModelState);
    }
}
