using Consular.Api.Data;
using Consular.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Services;

// Fetches the rows a report needs, then delegates the actual math to ReportCalculations (kept
// separate so that logic is unit-testable without a database — see ReportCalculationsTests).
public class ReportAggregationService
{
    private readonly AppDbContext _db;

    public ReportAggregationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ReportSummaryDto> BuildSummaryAsync(DateOnly from, DateOnly to, string? equipe, CancellationToken ct)
    {
        var demandes = await FetchDemandesAsync(from, to, equipe, ct);

        var byTypeService = demandes
            .GroupBy(d => (d.TypeService!.Code, d.TypeService.Libelle))
            .Select(g => new CountByLabelDto(g.Key.Code, g.Key.Libelle, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var byStatut = demandes
            .GroupBy(d => (d.Statut!.Code, d.Statut.Libelle))
            .Select(g => new CountByLabelDto(g.Key.Code, g.Key.Libelle, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var byRegion = demandes
            .GroupBy(d => d.EquipeAssignee)
            .Select(g => new CountByLabelDto(g.Key, g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var volumeOverTime = demandes
            .GroupBy(d => DateOnly.FromDateTime(d.DateDepot))
            .Select(g => new DailyCountDto(g.Key, g.Count()))
            .OrderBy(x => x.Date)
            .ToList();

        var processingTime = ReportCalculations.ComputeProcessingTime(demandes);

        // Scoped to the same date-range-filtered demandes (by DateDepot) rather than the
        // historique's own DateChangement range — "SLA compliance for cases submitted in this
        // period," matching how the other sections are scoped.
        var demandeIds = demandes.Select(d => d.Id).ToList();
        var historique = await _db.DemandeHistoriques.Where(h => demandeIds.Contains(h.DemandeId)).ToListAsync(ct);
        var sla = ReportCalculations.ComputeSlaCompliance(demandes, historique, DateTime.UtcNow);

        var staffActivity = await BuildStaffActivityAsync(from, to, ct);

        return new ReportSummaryDto(from, to, demandes.Count, byTypeService, byStatut, byRegion, volumeOverTime, processingTime, sla, staffActivity);
    }

    public async Task<List<ReportExportRowDto>> BuildExportRowsAsync(DateOnly from, DateOnly to, string? equipe, CancellationToken ct)
    {
        var demandes = await FetchDemandesAsync(from, to, equipe, ct);

        return demandes
            .OrderBy(d => d.DateDepot)
            .Select(d => new ReportExportRowDto(
                d.NumeroReference, d.TypeService!.Code, d.TypeService.Libelle, d.Statut!.Code, d.Statut.Libelle,
                d.EquipeAssignee, d.DateDepot, d.UpdatedAt,
                d.Statut.EstFinal ? Math.Round((d.UpdatedAt - d.DateDepot).TotalDays, 1) : null))
            .ToList();
    }

    private async Task<List<Consular.Shared.Entities.Demande>> FetchDemandesAsync(DateOnly from, DateOnly to, string? equipe, CancellationToken ct)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var query = _db.Demandes
            .Include(d => d.TypeService)
            .Include(d => d.Statut)
            .Where(d => d.DateDepot >= fromUtc && d.DateDepot <= toUtc);

        if (!string.IsNullOrWhiteSpace(equipe)) query = query.Where(d => d.EquipeAssignee == equipe);

        return await query.ToListAsync(ct);
    }

    // Semi-join against Users.Nom (rather than a hardcoded exclude-list of the non-staff
    // ActorName sentinels — "applicant", "applicant (appeal)", "applicant (documents
    // submitted)", "system") since DemandesController.Transition sets ActorName to the acting
    // staff member's own User.Nom — this naturally only counts real staff transitions.
    private async Task<List<StaffActivityDto>> BuildStaffActivityAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var staffNames = await _db.Users.Select(u => u.Nom).ToListAsync(ct);

        // GroupBy().Select(g => new StaffActivityDto(...)) doesn't translate to SQL here (EF
        // Core can't push a record constructor through the grouped Count() in this shape) — group
        // into a plain anonymous type in SQL, then materialize before building the DTO.
        var counts = await _db.DemandeHistoriques
            .Where(h => h.DateChangement >= fromUtc && h.DateChangement <= toUtc && h.ActorName != null && staffNames.Contains(h.ActorName))
            .GroupBy(h => h.ActorName!)
            .Select(g => new { ActorName = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts
            .OrderByDescending(x => x.Count)
            .Select(x => new StaffActivityDto(x.ActorName, x.Count))
            .ToList();
    }
}
