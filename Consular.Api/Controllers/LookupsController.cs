using Consular.Api.Data;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Consular.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Controllers;

// Read-only reference data consumed by both the public site (service catalog on the home
// page, submission form) and the staff dashboard (status filters/labels).
[ApiController]
[Route("api/v1/lookups")]
[AllowAnonymous]
public class LookupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<LookupsController> _logger;

    public LookupsController(AppDbContext db, ILogger<LookupsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("types-service")]
    public async Task<ActionResult<List<TypeServiceDto>>> GetTypesService()
    {
        var types = await _db.TypeServices
            .Where(t => t.Actif)
            .OrderBy(t => t.Libelle)
            .Select(t => new TypeServiceDto(t.Id, t.Code, t.Libelle, t.Categorie, t.Description, t.MontantFrais, t.DocumentsRequisFr, t.DocumentsRequisEn))
            .ToListAsync();

        _logger.LogDebug("Returned {Count} active type(s) de service", types.Count);
        return Ok(types);
    }

    [HttpGet("statuts")]
    public async Task<ActionResult<List<StatutDto>>> GetStatuts()
    {
        var statuts = await _db.Statuts
            .Where(s => s.Actif)
            .OrderBy(s => s.Ordre)
            .Select(s => new StatutDto(s.Id, s.Code, s.Libelle, s.Ordre, s.EstFinal))
            .ToListAsync();

        _logger.LogDebug("Returned {Count} active statut(s)", statuts.Count);
        return Ok(statuts);
    }

    [HttpGet("regions")]
    public async Task<ActionResult<List<RegionLookupDto>>> GetRegions()
    {
        var regions = await _db.RegionLookups
            .Where(r => r.Actif)
            .OrderBy(r => r.Ordre)
            .Select(r => new RegionLookupDto(r.Id, r.Code, r.Valeur, r.LibelleFr, r.LibelleEn, r.Ordre))
            .ToListAsync();

        _logger.LogDebug("Returned {Count} active region(s)", regions.Count);
        return Ok(regions);
    }

    // Which weekdays/times are open at all for a region+category — used to build the
    // applicant-facing calendar before any date is picked (e.g. flag a Sunday as closed).
    // Capacity is scoped per category too — a Passeport slot and a Visa slot at the same
    // region/day/time are different resources, not one shared pool.
    [HttpGet("appointment-slots/weekly-template")]
    public async Task<ActionResult<List<AppointmentSlotWeeklyTemplateDto>>> GetAppointmentSlotWeeklyTemplate(int region, TypeServiceCategorie categorie)
    {
        var templates = await _db.AppointmentSlotTemplates
            .Where(t => t.Actif && (int)t.Region == region && t.Categorie == categorie)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Select(t => new AppointmentSlotWeeklyTemplateDto(t.DayOfWeek, t.StartTime, t.CapaciteMax))
            .ToListAsync();

        _logger.LogDebug("Returned {Count} weekly appointment slot template row(s) for region {Region}, categorie {Categorie}", templates.Count, region, categorie);
        return Ok(templates);
    }

    // Live remaining capacity for one specific calendar date — once the applicant has picked a
    // date, this drives the list of pickable times. See AppointmentSlotAvailability for the pure
    // computation and DemandesController.Create for the authoritative re-check at booking time
    // (this GET is necessarily best-effort/stale by the time a POST actually lands).
    [HttpGet("appointment-slots/availability")]
    public async Task<ActionResult<List<AppointmentSlotAvailabilityDto>>> GetAppointmentSlotAvailability(int region, TypeServiceCategorie categorie, DateOnly date)
    {
        var dayOfWeek = date.DayOfWeek;
        var templates = await _db.AppointmentSlotTemplates
            .Where(t => t.Actif && (int)t.Region == region && t.Categorie == categorie && t.DayOfWeek == dayOfWeek)
            .ToListAsync();
        if (templates.Count == 0) return Ok(new List<AppointmentSlotAvailabilityDto>());

        var regionLabel = await GetRegionLabelAsync((Region)region);
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);

        // A REJECTED case releases its slot; every other status (including one that's since
        // moved past WAITING_BIOMETRICS) still holds it.
        var booked = await _db.Demandes
            .Where(d => d.EquipeAssignee == regionLabel && d.TypeService!.Categorie == categorie
                && d.RendezVousBiometrieAt >= dayStartUtc && d.RendezVousBiometrieAt < dayEndUtc
                && d.Statut!.Code != "REJECTED")
            .Select(d => d.RendezVousBiometrieAt!.Value)
            .ToListAsync();

        var availability = AppointmentSlotAvailability.ComputeForDate(templates, booked)
            .Select(a => new AppointmentSlotAvailabilityDto(a.StartTime, a.CapaciteMax, a.Remaining))
            .ToList();

        _logger.LogDebug("Returned availability for {Count} appointment slot(s) on {Date} (region {Region}, categorie {Categorie})", availability.Count, date, region, categorie);
        return Ok(availability);
    }

    // RegionLookup.Valeur mirrors the Region enum's ordinal (North=0/Lagos, South=1/Abuja), so a
    // Region can be resolved to its human label ("Lagos"/"Abuja") without duplicating that
    // mapping as a second hardcoded table. Same helper as DemandesController's private one —
    // small enough (and used by only these two controllers) that sharing it isn't worth a new
    // abstraction.
    private async Task<string> GetRegionLabelAsync(Region region)
    {
        var label = await _db.RegionLookups
            .Where(r => r.Valeur == (int)region)
            .Select(r => r.LibelleFr)
            .FirstOrDefaultAsync();
        return label ?? region.ToString();
    }
}
