using Consular.Api.Dtos;
using Consular.Shared.Entities;

namespace Consular.Api.Services;

// Pure computation over already-fetched entities — no AppDbContext here, so this is unit-testable
// the same way DemandeWorkflowRules is (see DemandeWorkflowRulesTests): construct plain Demande/
// Statut/TypeService/DemandeHistorique objects and call these directly. ReportAggregationService
// does the actual EF querying and delegates the math to this class.
public static class ReportCalculations
{
    // "Closed" means EstFinal (COLLECTED is the only such Statut today — see Statut.EstFinal).
    // REJECTED is deliberately not "closed" here for the same reason it isn't a dead end
    // elsewhere in the app: an appeal can still reopen it.
    public static ProcessingTimeDto ComputeProcessingTime(IReadOnlyCollection<Demande> demandes)
    {
        var closed = demandes.Where(d => d.Statut!.EstFinal).ToList();
        if (closed.Count == 0)
        {
            return new ProcessingTimeDto(null, 0, new List<CategoryProcessingTimeDto>());
        }

        var overallAverage = closed.Average(d => (d.UpdatedAt - d.DateDepot).TotalDays);

        var byCategory = closed
            .GroupBy(d => d.TypeService!.Categorie)
            .Select(g => new CategoryProcessingTimeDto(g.Key.ToString(), Math.Round(g.Average(d => (d.UpdatedAt - d.DateDepot).TotalDays), 1), g.Count()))
            .OrderBy(x => x.Categorie)
            .ToList();

        return new ProcessingTimeDto(Math.Round(overallAverage, 1), closed.Count, byCategory);
    }

    // The "on time" counts are exact, not estimated: DemandesController.SubmitMissingDocuments
    // and .Appeal both reject a late attempt with a 409 and persist nothing, so a
    // MISSING_DOCUMENTS -> DOCUMENTS_RECEIVED or REJECTED -> APPEAL_REVIEW historique row can
    // only exist if it happened on time. "Missing documents missed" is equally exact — it's
    // MissingDocumentsExpiryService's own auto-rejection rows (ActorName "system"). There's no
    // equivalent auto-sweep for missed *appeal* deadlines, so that one has no stored fact to
    // count — it's derived live: demandes currently REJECTED whose 10-working-day window has
    // already elapsed as of `now`.
    public static SlaComplianceDto ComputeSlaCompliance(IReadOnlyCollection<Demande> demandes, IReadOnlyCollection<DemandeHistorique> historique, DateTime now)
    {
        var missingDocumentsOnTime = historique.Count(h => h.ActorName == "applicant (documents submitted)");
        var missingDocumentsMissed = historique.Count(h => h.ActorName == "system");
        var appealsOnTime = historique.Count(h => h.ActorName == "applicant (appeal)");

        var appealsMissed = demandes.Count(d =>
            d.Statut!.Code == "REJECTED" &&
            WorkingDays.AddWorkingDays(d.UpdatedAt, WorkingDays.AppealAndDocumentsDeadlineDays) < now);

        return new SlaComplianceDto(missingDocumentsOnTime, missingDocumentsMissed, appealsOnTime, appealsMissed);
    }
}
