namespace Consular.Api.Dtos;

// One cohesive response for the whole Reports tab — a single fetch renders every section, rather
// than the frontend orchestrating several requests against a shared date range.
public record ReportSummaryDto(
    DateOnly From,
    DateOnly To,
    int TotalDemandes,
    List<CountByLabelDto> ByTypeService,
    List<CountByLabelDto> ByStatut,
    List<CountByLabelDto> ByRegion,
    List<DailyCountDto> VolumeOverTime,
    ProcessingTimeDto ProcessingTime,
    SlaComplianceDto SlaCompliance,
    List<StaffActivityDto> StaffActivity
);

public record CountByLabelDto(string Code, string Label, int Count);

public record DailyCountDto(DateOnly Date, int Count);

public record ProcessingTimeDto(double? AverageDays, int ClosedCaseCount, List<CategoryProcessingTimeDto> ByCategory);

public record CategoryProcessingTimeDto(string Categorie, double AverageDays, int Count);

// MissingDocuments*/Appeals* pairs come from data the app already guarantees is accurate — see
// ReportCalculations.ComputeSlaCompliance for exactly which historique rows/live checks back
// each count.
public record SlaComplianceDto(int MissingDocumentsOnTime, int MissingDocumentsMissed, int AppealsOnTime, int AppealsMissed);

public record StaffActivityDto(string ActorName, int TransitionCount);

// One row of the raw export (CSV/XLSX) — deliberately the underlying data, not the aggregates
// already shown by ReportSummaryDto, so staff can pivot it themselves.
public record ReportExportRowDto(
    string NumeroReference,
    string TypeServiceCode,
    string TypeServiceLibelle,
    string StatutCode,
    string StatutLibelle,
    string EquipeAssignee,
    DateTime DateDepot,
    DateTime UpdatedAt,
    double? ProcessingDays
);
