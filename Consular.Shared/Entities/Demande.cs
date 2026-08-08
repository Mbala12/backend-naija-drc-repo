using System.Text.Json;

namespace Consular.Shared.Entities;

public class Demande
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Human-friendly tracking number the citizen can quote over phone/email. Format: DEM-2026-111001
    // (the middle 3 digits identify the service type — see NumeroReferenceGenerator).
    public string NumeroReference { get; set; } = string.Empty;

    public Guid TypeServiceId { get; set; }
    public TypeService? TypeService { get; set; }

    public Guid ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    // The account that submitted this demande — distinct from ApplicantId (the beneficiary),
    // since either the applicant themselves or a staff User can submit on behalf of someone
    // else. Exactly one of these two is set, never both.
    public Guid? SoumisParApplicantId { get; set; }
    public Applicant? SoumisParApplicant { get; set; }
    public Guid? SoumisParUserId { get; set; }
    public User? SoumisParUser { get; set; }

    public Guid StatutId { get; set; }
    public Statut? Statut { get; set; }

    public string CanalDepot { get; set; } = "web";

    // Flexible bag for fields not covered by a dedicated extension table (jsonb).
    public JsonDocument? Attributs { get; set; }

    // Which region currently owns the dossier (e.g. "Lagos"/"Abuja") — set from the acting
    // staff member's own Region on every transition (see DemandesController), defaulting to
    // Lagos at submission since no staff has acted on it yet.
    public string EquipeAssignee { get; set; } = "Lagos";

    // Free-text note set by staff when documents are missing; cleared once the citizen resubmits.
    public string? NoteDocumentsManquantes { get; set; }

    // Free-text reason set by staff when a request is rejected; cleared once the case leaves
    // REJECTED (appealed, or re-rejected with a fresh reason after an appeal review).
    public string? NoteRejet { get; set; }

    // The applicant's requested appointment, for any service category — set at submission time
    // (see DemandesController.Create and AppointmentSlotAvailability). Field name is historical
    // (this started as passport-only biometrics booking): for Passeport, staff can keep it or
    // overwrite it when transitioning to WAITING_BIOMETRICS (see DemandesController.Transition),
    // which is the moment the confirmation email actually goes out (see IEmailService); for
    // Visa/EtatCivil it's informational only, with no equivalent staff re-confirm step.
    public DateTime? RendezVousBiometrieAt { get; set; }

    public DateTime DateDepot { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DemandeVisa? DemandeVisa { get; set; }
    public DemandeEtatCivil? DemandeEtatCivil { get; set; }
    public DemandePasseport? DemandePasseport { get; set; }
    public ICollection<DemandeDocument> Documents { get; set; } = new List<DemandeDocument>();
    public ICollection<DemandeHistorique> Historique { get; set; } = new List<DemandeHistorique>();
}
