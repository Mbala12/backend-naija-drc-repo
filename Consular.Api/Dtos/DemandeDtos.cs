using System.ComponentModel.DataAnnotations;
using Consular.Shared.Enums;

namespace Consular.Api.Dtos;

public record CreateDemandeDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string CitoyenNom,
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email)] string CitoyenEmail,
    [Required(ErrorMessage = ValidationMessages.Required), Phone(ErrorMessage = ValidationMessages.Phone)] string CitoyenTelephone,
    [Required(ErrorMessage = ValidationMessages.Required)] string TypeServiceCode,
    string CanalDepot = "web",
    // Populated only when the selected TypeService is in the Visa category. TypeVisa is no
    // longer client-supplied — each visa sub-type (Touristique/Affaires/...) is now its own
    // TypeService (see DemoDataSeeder), so DemandesController derives it from TypeService.Libelle.
    string? PaysDestination = null,
    DateOnly? DateEntreePrevue = null,
    int? DureeSejourJours = null,
    // Populated only when the selected TypeService is in the EtatCivil category. TypeActe is
    // likewise derived server-side now, for the same reason.
    DateOnly? DateEvenement = null,
    string? LieuEvenement = null,
    // Populated only when the selected TypeService is in the Passeport category.
    // NumeroPasseportActuel/DateExpirationActuelle are optional even then — a first-time
    // "Nouveau" request has no prior passport to reference.
    string? TypeDemandePasseport = null,
    string? NumeroPasseportActuel = null,
    DateOnly? DateExpirationActuelle = null,
    string? Nationalite = null,
    // Required for every request, regardless of category: the applicant's requested appointment
    // (region/date/time), validated and booked by DemandesController.Create against
    // AppointmentSlotTemplate — see AppointmentSlotAvailability. RendezVousRegion is the Region
    // enum ordinal (0=North/Lagos, 1=South/Abuja). For Passeport this is a request, not a
    // confirmation — staff still review it via the existing Transition "waiting-biometrics"
    // action before it's final; for Visa/EtatCivil it's informational once booked.
    int? RendezVousRegion = null,
    DateOnly? RendezVousDate = null,
    TimeOnly? RendezVousHeure = null
);

public record DemandeVisaDto(string TypeVisa, string PaysDestination, DateOnly DateEntreePrevue, int DureeSejourJours);

public record DemandeEtatCivilDto(string TypeActe, DateOnly DateEvenement, string LieuEvenement);

public record DemandePasseportDto(string TypeDemande, string? NumeroPasseportActuel, DateOnly? DateExpirationActuelle);

public record DemandeSummaryDto(
    Guid Id,
    string NumeroReference,
    string TypeServiceCode,
    string TypeServiceLibelle,
    TypeServiceCategorie TypeServiceCategorie,
    string StatutCode,
    string StatutLibelle,
    string CanalDepot,
    string? NoteDocumentsManquantes,
    string? NoteRejet,
    string EquipeAssignee,
    DateTime DateDepot,
    DateTime UpdatedAt,
    string? SoumisParNom,
    string ApplicantNom,
    // Set for a Passeport demande once a biometrics appointment has been requested/confirmed —
    // needed here (not just on DemandeDetailDto) so the staff dashboard's list view can pre-fill
    // the appointment picker with the applicant's original request during review.
    DateTime? RendezVousBiometrieAt = null
);

public record DemandeDetailDto(
    Guid Id,
    string NumeroReference,
    string CitoyenNom,
    string CitoyenEmail,
    string CitoyenTelephone,
    string TypeServiceCode,
    string TypeServiceLibelle,
    TypeServiceCategorie TypeServiceCategorie,
    string StatutCode,
    string StatutLibelle,
    string CanalDepot,
    string? NoteDocumentsManquantes,
    string? NoteRejet,
    string EquipeAssignee,
    DateTime DateDepot,
    DateTime UpdatedAt,
    DemandeVisaDto? Visa,
    DemandeEtatCivilDto? EtatCivil,
    DemandePasseportDto? Passeport,
    List<DocumentSummaryDto> Documents,
    List<HistoriqueDto> Historique,
    string? SoumisParNom,
    DateTime? RendezVousBiometrieAt
);

public record HistoriqueDto(string? StatutOrigineCode, string StatutDestinationCode, string? ActorName, string? Details, DateTime DateChangement);

public record DocumentUploadResultDto(Guid Id, string FileName, string CheminStockage, string DocumentKind, DateTime UploadedAt);

public record DocumentSummaryDto(Guid Id, string FileName, string CheminStockage, string DocumentKind, bool ValideParAgent, DateTime UploadedAt);

// ActorName is derived from the authenticated staff member's JWT claims by the controller;
// it isn't accepted from the client. RendezVousAt is only meaningful (and required — see
// DemandesController.Transition) for the "waiting-biometrics" action on a Passeport demande.
public record TransitionDemandeDto([Required(ErrorMessage = ValidationMessages.Required)] string Action, string? Details, DateTime? RendezVousAt = null);

// Applicant self-service: appealing a REJECTED case (see DemandesController.Appeal). Anonymous
// like the tracking/upload endpoints — the Demande's Id (a GUID) is the only credential, same
// trust model as document upload.
public record AppealDemandeDto(string? Reason);

// Applicant self-service: confirming missing documents were uploaded (see
// DemandesController.SubmitMissingDocuments), taking MISSING_DOCUMENTS to APPEAL_REVIEW.
public record SubmitMissingDocumentsDto(string? Details);
