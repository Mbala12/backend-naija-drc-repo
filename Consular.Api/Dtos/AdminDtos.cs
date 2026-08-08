using System.ComponentModel.DataAnnotations;

namespace Consular.Api.Dtos;

// Flat, standalone views of the child tables — each row carries the parent Demande's
// NumeroReference (rather than a raw DemandeId) since that's what's meaningful to a
// Nord account browsing "every table" outside the context of any single demande.
public record DocumentAdminDto(Guid Id, string NumeroReference, string FileName, string CheminStockage, string DocumentKind, bool ValideParAgent, DateTime UploadedAt);

public record HistoriqueAdminDto(long Id, string NumeroReference, string? StatutOrigineCode, string StatutDestinationCode, string? ActorName, string? Details, DateTime DateChangement);

public record AuditLogAdminDto(long Id, string ActorName, string Action, string EntityType, string EntityId, string? Details, DateTime CreatedAt);

public record VisaAdminDto(Guid DemandeId, string NumeroReference, string TypeVisa, string PaysDestination, DateOnly DateEntreePrevue, int DureeSejourJours);

public record EtatCivilAdminDto(Guid DemandeId, string NumeroReference, string TypeActe, DateOnly DateEvenement, string LieuEvenement);

public record PasseportAdminDto(Guid DemandeId, string NumeroReference, string TypeDemande, string? NumeroPasseportActuel, DateOnly? DateExpirationActuelle);

// --- Edit payloads --------------------------------------------------------------------------
// Only descriptive fields are editable — IDs, foreign keys, and system-managed timestamps
// (and, for Demande, the Statut itself) stay off-limits here since those flow through their
// own dedicated logic elsewhere (e.g. the workflow transition endpoint).

public record UpdateApplicantAdminDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Nom,
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email)] string Email,
    [Required(ErrorMessage = ValidationMessages.Required), Phone(ErrorMessage = ValidationMessages.Phone)] string Telephone,
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Nationalite
);

public record UpdateTypeServiceAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string Libelle, string? Description, bool Actif, [Range(0, double.MaxValue, ErrorMessage = ValidationMessages.Range)] decimal MontantFrais);

public record UpdateStatutAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string Libelle, int Ordre, bool EstFinal, bool Actif);

public record UpdateDemandeAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string CanalDepot, string? NoteDocumentsManquantes, [Required(ErrorMessage = ValidationMessages.Required)] string EquipeAssignee);

public record UpdateDocumentAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string DocumentKind, bool ValideParAgent);

public record UpdateHistoriqueAdminDto(string? ActorName, string? Details);

public record UpdateVisaAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string TypeVisa, [Required(ErrorMessage = ValidationMessages.Required)] string PaysDestination, DateOnly DateEntreePrevue, int DureeSejourJours);

public record UpdateEtatCivilAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string TypeActe, DateOnly DateEvenement, [Required(ErrorMessage = ValidationMessages.Required)] string LieuEvenement);

public record UpdatePasseportAdminDto([Required(ErrorMessage = ValidationMessages.Required)] string TypeDemande, string? NumeroPasseportActuel, DateOnly? DateExpirationActuelle);
