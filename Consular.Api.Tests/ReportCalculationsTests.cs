using Consular.Api.Services;
using Consular.Shared.Entities;
using Consular.Shared.Enums;
using Xunit;

namespace Consular.Api.Tests;

public class ReportCalculationsTests
{
    private static Statut MakeStatut(string code, bool estFinal) => new() { Code = code, Libelle = code, EstFinal = estFinal };
    private static TypeService MakeTypeService(TypeServiceCategorie categorie) => new() { Code = "X", Libelle = "X", Categorie = categorie };

    private static Demande MakeDemande(Statut statut, TypeServiceCategorie categorie, DateTime dateDepot, DateTime updatedAt) => new()
    {
        Statut = statut,
        TypeService = MakeTypeService(categorie),
        DateDepot = dateDepot,
        UpdatedAt = updatedAt
    };

    [Fact]
    public void ComputeProcessingTime_NoClosedCases_ReturnsNullAverage()
    {
        var collected = MakeStatut("UNDER_REVIEW", estFinal: false);
        var demandes = new[] { MakeDemande(collected, TypeServiceCategorie.Visa, DateTime.UtcNow, DateTime.UtcNow) };

        var result = ReportCalculations.ComputeProcessingTime(demandes);

        Assert.Null(result.AverageDays);
        Assert.Equal(0, result.ClosedCaseCount);
        Assert.Empty(result.ByCategory);
    }

    [Fact]
    public void ComputeProcessingTime_OnlyAveragesClosedCases_GroupedByCategory()
    {
        var collected = MakeStatut("COLLECTED", estFinal: true);
        var underReview = MakeStatut("UNDER_REVIEW", estFinal: false);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var demandes = new[]
        {
            MakeDemande(collected, TypeServiceCategorie.Visa, start, start.AddDays(4)), // 4 days
            MakeDemande(collected, TypeServiceCategorie.Visa, start, start.AddDays(6)), // 6 days -> Visa avg 5
            MakeDemande(collected, TypeServiceCategorie.Passeport, start, start.AddDays(10)), // 10 days -> Passeport avg 10
            MakeDemande(underReview, TypeServiceCategorie.Visa, start, start.AddDays(100)) // not closed, excluded
        };

        var result = ReportCalculations.ComputeProcessingTime(demandes);

        Assert.Equal(3, result.ClosedCaseCount);
        // ComputeProcessingTime rounds AverageDays to 1 decimal place for display — 20/3 = 6.67 -> 6.7.
        Assert.Equal(6.7, result.AverageDays!.Value, 1);
        var visaCategory = Assert.Single(result.ByCategory, c => c.Categorie == TypeServiceCategorie.Visa.ToString());
        Assert.Equal(5, visaCategory.AverageDays);
        Assert.Equal(2, visaCategory.Count);
        var passeportCategory = Assert.Single(result.ByCategory, c => c.Categorie == TypeServiceCategorie.Passeport.ToString());
        Assert.Equal(10, passeportCategory.AverageDays);
    }

    [Fact]
    public void ComputeSlaCompliance_CountsOnTimeTransitionsAndSystemAutoRejections()
    {
        var demandes = Array.Empty<Demande>();
        var historique = new[]
        {
            new DemandeHistorique { ActorName = "applicant (documents submitted)" },
            new DemandeHistorique { ActorName = "applicant (documents submitted)" },
            new DemandeHistorique { ActorName = "system" },
            new DemandeHistorique { ActorName = "applicant (appeal)" },
            new DemandeHistorique { ActorName = "Admin Nord" } // a staff transition — shouldn't count toward any SLA bucket
        };

        var result = ReportCalculations.ComputeSlaCompliance(demandes, historique, DateTime.UtcNow);

        Assert.Equal(2, result.MissingDocumentsOnTime);
        Assert.Equal(1, result.MissingDocumentsMissed);
        Assert.Equal(1, result.AppealsOnTime);
        Assert.Equal(0, result.AppealsMissed);
    }

    [Fact]
    public void ComputeSlaCompliance_FlagsRejectedCasesPastTheAppealDeadline()
    {
        var rejected = MakeStatut("REJECTED", estFinal: false);
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        // Rejected 20 days ago (well past the 10-working-day appeal window) -> missed.
        var overdue = MakeDemande(rejected, TypeServiceCategorie.Visa, now.AddDays(-30), now.AddDays(-20));
        // Rejected yesterday -> still within the window.
        var withinWindow = MakeDemande(rejected, TypeServiceCategorie.Visa, now.AddDays(-2), now.AddDays(-1));

        var result = ReportCalculations.ComputeSlaCompliance(new[] { overdue, withinWindow }, Array.Empty<DemandeHistorique>(), now);

        Assert.Equal(1, result.AppealsMissed);
    }
}
