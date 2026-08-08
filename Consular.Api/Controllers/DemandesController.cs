using System.Security.Claims;
using Consular.Api.Data;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Consular.Shared.Entities;
using Consular.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UserEntity = Consular.Shared.Entities.User;

namespace Consular.Api.Controllers;

// UserEntity alias: ControllerBase already exposes a `User` property (the ClaimsPrincipal)
// which would otherwise shadow the `Consular.Shared.Entities.User` type name.
[ApiController]
[Route("api/v1/demandes")]
public class DemandesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDocumentStorageService _documentStorage;
    private readonly IEmailService _emailService;
    private readonly ILogger<DemandesController> _logger;

    public DemandesController(
        AppDbContext db,
        IDocumentStorageService documentStorage,
        IEmailService emailService,
        ILogger<DemandesController> logger)
    {
        _db = db;
        _documentStorage = documentStorage;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<DemandeDetailDto>> Create(CreateDemandeDto dto)
    {
        // The caller is either a self-service Applicant or a staff User submitting on behalf
        // of someone — the Role claim says which table to resolve the submitter from. Using
        // FirstOrDefaultAsync (not FirstAsync) here deliberately: a stale/mismatched token
        // must produce a clean 401, not an unhandled "sequence contains no matching element".
        var submitterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var submitterRole = User.FindFirstValue(ClaimTypes.Role);

        Applicant? submitterApplicant = null;
        UserEntity? submitterUser = null;
        string submitterEmail;

        if (submitterRole == "Applicant")
        {
            submitterApplicant = await _db.Applicants.FirstOrDefaultAsync(a => a.Id == submitterId);
            if (submitterApplicant is null) return Unauthorized();
            submitterEmail = submitterApplicant.Email;
        }
        else
        {
            submitterUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == submitterId);
            if (submitterUser is null) return Unauthorized();
            submitterEmail = submitterUser.Email;
        }

        var typeService = await _db.TypeServices.FirstOrDefaultAsync(t => t.Code == dto.TypeServiceCode && t.Actif);
        if (typeService is null)
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: unknown or inactive type de service {Code}",
                submitterEmail, dto.TypeServiceCode);
            return ValidationProblem($"Type de service inconnu ou inactif « {dto.TypeServiceCode} ».");
        }

        if (typeService.Categorie == TypeServiceCategorie.Visa &&
            (dto.PaysDestination is null || dto.DateEntreePrevue is null || dto.DureeSejourJours is null))
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: missing visa fields for {Code}", submitterEmail, dto.TypeServiceCode);
            return ValidationProblem("Le pays de destination, la date d'entrée prévue et la durée du séjour sont requis pour une demande de visa.");
        }

        if (typeService.Categorie == TypeServiceCategorie.EtatCivil &&
            (dto.DateEvenement is null || dto.LieuEvenement is null))
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: missing état civil fields for {Code}", submitterEmail, dto.TypeServiceCode);
            return ValidationProblem("La date et le lieu de l'événement sont requis pour une demande de certificat.");
        }

        if (typeService.Categorie == TypeServiceCategorie.Passeport && dto.TypeDemandePasseport is null)
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: missing passeport fields for {Code}", submitterEmail, dto.TypeServiceCode);
            return ValidationProblem("Le type de demande de passeport est requis pour une demande de passeport.");
        }

        // An appointment request (region/date/time) is required for every demande, regardless of
        // category — see AppointmentSlotAvailability for the capacity math and the re-validation
        // right before the Demande is built below. This is a *request*, not a confirmation: for
        // Passeport, staff still review it via the existing Transition "waiting-biometrics"
        // action; for Visa/EtatCivil (which have no such staff step) it's informational once
        // booked.
        if (dto.RendezVousRegion is null || dto.RendezVousDate is null || dto.RendezVousHeure is null)
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: missing appointment fields for {Code}", submitterEmail, dto.TypeServiceCode);
            return ValidationProblem("Un rendez-vous (région, date et heure) est requis.");
        }
        if (!Enum.IsDefined(typeof(Region), dto.RendezVousRegion.Value))
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: unknown appointment region {Region}", submitterEmail, dto.RendezVousRegion);
            return ValidationProblem("Région inconnue pour le rendez-vous.");
        }
        if (dto.RendezVousDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            _logger.LogWarning("Demande creation by {Submitter} rejected: appointment date {Date} is in the past", submitterEmail, dto.RendezVousDate);
            return ValidationProblem("La date du rendez-vous ne peut pas être dans le passé.");
        }

        // The beneficiary (who the demande is FOR) is always an Applicant. If the submitter IS
        // an applicant and the form's email points at themselves, they're their own beneficiary;
        // otherwise (a different email, or the submitter is a staff User) find-or-create the
        // beneficiary Applicant record.
        var isOnBehalfOf = submitterApplicant is null || !string.Equals(dto.CitoyenEmail, submitterApplicant.Email, StringComparison.OrdinalIgnoreCase);
        Applicant applicant;
        if (!isOnBehalfOf)
        {
            applicant = submitterApplicant!;
        }
        else
        {
            var beneficiaire = await _db.Applicants.FirstOrDefaultAsync(a => a.Email == dto.CitoyenEmail);
            if (beneficiaire is null)
            {
                if (!await _db.HasCapacityForNewAccountAsync())
                {
                    _logger.LogWarning("Demande creation by {Submitter} rejected: account limit reached, cannot create beneficiary {Email}", submitterEmail, dto.CitoyenEmail);
                    return ValidationProblem($"La limite de {AccountCapacityExtensions.MaxAccounts} comptes pour cette démo a été atteinte. Supprimez un postulant ou un utilisateur existant avant d'en ajouter un autre.");
                }
                beneficiaire = new Applicant { Email = dto.CitoyenEmail };
                _db.Applicants.Add(beneficiaire);
                _logger.LogInformation("New beneficiary-only applicant record created for {Email} by {Submitter}", dto.CitoyenEmail, submitterEmail);
            }
            applicant = beneficiaire;
        }
        applicant.Nom = dto.CitoyenNom;
        applicant.Telephone = dto.CitoyenTelephone;
        applicant.Nationalite = dto.Nationalite ?? string.Empty;

        var statutCode = "SUBMITTED";
        // The applicant's chosen embassy location IS the region — resolved (and re-validated for
        // capacity, scoped per Region+Categorie) below — for every category now, not just
        // Passeport. No staff has touched this dossier yet, so nothing else here influences
        // EquipeAssignee (see GetRegionLabelAsync).
        string equipe;
        DateTime? rendezVousAt;
        {
            var region = (Region)dto.RendezVousRegion!.Value;
            var dayOfWeek = dto.RendezVousDate!.Value.DayOfWeek;
            var templates = await _db.AppointmentSlotTemplates
                .Where(t => t.Actif && t.Region == region && t.Categorie == typeService.Categorie && t.DayOfWeek == dayOfWeek)
                .ToListAsync();

            equipe = await GetRegionLabelAsync(region);
            var dayStartUtc = dto.RendezVousDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEndUtc = dayStartUtc.AddDays(1);
            var booked = await _db.Demandes
                .Where(d => d.EquipeAssignee == equipe && d.TypeService!.Categorie == typeService.Categorie
                    && d.RendezVousBiometrieAt >= dayStartUtc && d.RendezVousBiometrieAt < dayEndUtc
                    && d.Statut!.Code != "REJECTED")
                .Select(d => d.RendezVousBiometrieAt!.Value)
                .ToListAsync();

            // The source of truth: the availability the client fetched earlier (to render the
            // picker) can be stale by now, so re-check here rather than trusting the client.
            if (!AppointmentSlotAvailability.TryValidateSlot(templates, booked, dto.RendezVousHeure!.Value, out _))
            {
                _logger.LogWarning("Demande creation by {Submitter} rejected: appointment slot no longer available ({Region} {Categorie} {Date} {Time})",
                    submitterEmail, region, typeService.Categorie, dto.RendezVousDate, dto.RendezVousHeure);
                return Problem("Ce créneau de rendez-vous n'est plus disponible. Veuillez choisir une autre date ou heure.", statusCode: StatusCodes.Status409Conflict);
            }

            rendezVousAt = dto.RendezVousDate.Value.ToDateTime(dto.RendezVousHeure.Value, DateTimeKind.Utc);
        }
        var statut = await _db.Statuts.FirstOrDefaultAsync(s => s.Code == statutCode)
            ?? throw new InvalidOperationException($"Statut '{statutCode}' is not seeded.");

        var demande = new Demande
        {
            NumeroReference = await GenerateNumeroReferenceAsync(typeService),
            TypeServiceId = typeService.Id,
            Applicant = applicant,
            SoumisParApplicantId = submitterApplicant?.Id,
            SoumisParUserId = submitterUser?.Id,
            StatutId = statut.Id,
            CanalDepot = dto.CanalDepot,
            EquipeAssignee = equipe,
            RendezVousBiometrieAt = rendezVousAt
        };
        _db.Demandes.Add(demande);

        if (typeService.Categorie == TypeServiceCategorie.Visa)
        {
            _db.DemandeVisas.Add(new DemandeVisa
            {
                Demande = demande,
                // Derived from the specific TypeService picked (Touristique/Affaires/...) rather
                // than a separate client-supplied value — see CreateDemandeDto.
                TypeVisa = typeService.Libelle,
                PaysDestination = dto.PaysDestination!,
                DateEntreePrevue = dto.DateEntreePrevue!.Value,
                DureeSejourJours = dto.DureeSejourJours!.Value
            });
        }
        else if (typeService.Categorie == TypeServiceCategorie.EtatCivil)
        {
            _db.DemandeEtatCivils.Add(new DemandeEtatCivil
            {
                Demande = demande,
                TypeActe = typeService.Libelle,
                DateEvenement = dto.DateEvenement!.Value,
                LieuEvenement = dto.LieuEvenement!
            });
        }
        else if (typeService.Categorie == TypeServiceCategorie.Passeport)
        {
            _db.DemandePasseports.Add(new DemandePasseport
            {
                Demande = demande,
                TypeDemande = dto.TypeDemandePasseport!,
                NumeroPasseportActuel = dto.NumeroPasseportActuel,
                DateExpirationActuelle = dto.DateExpirationActuelle
            });
        }

        _db.DemandeHistoriques.Add(new DemandeHistorique
        {
            DemandeId = demande.Id,
            StatutOrigineId = null,
            StatutDestinationId = statut.Id,
            ActorName = "applicant",
            Details = $"Déposée via le portail web ({typeService.Code})"
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Demande {NumeroReference} ({TypeService}) created for {BeneficiaryEmail} by {SubmitterEmail}{OnBehalf}",
            demande.NumeroReference, typeService.Code, applicant.Email, submitterEmail,
            isOnBehalfOf ? " (on behalf of)" : "");

        var detail = await LoadDetailAsync(demande.Id) ?? throw new InvalidOperationException("Demande just created is missing.");
        return CreatedAtAction(nameof(GetByNumeroReference), new { numeroReference = demande.NumeroReference }, detail);
    }

    [HttpPost("{id:guid}/documents")]
    [AllowAnonymous]
    public async Task<ActionResult<DocumentUploadResultDto>> UploadDocument(Guid id, IFormFile file, [FromForm] string? documentKind)
    {
        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("Document upload rejected for demande {DemandeId}: no file provided", id);
            return BadRequest(new { message = "Un fichier est requis." });
        }

        var demande = await _db.Demandes.FirstOrDefaultAsync(d => d.Id == id);
        if (demande is null)
        {
            _logger.LogWarning("Document upload rejected: demande {DemandeId} not found", id);
            return NotFound();
        }

        string storageUrl;
        await using (var stream = file.OpenReadStream())
        {
            storageUrl = _documentStorage.SaveUpload(file.FileName, stream);
        }

        var document = new DemandeDocument
        {
            DemandeId = demande.Id,
            FileName = file.FileName,
            CheminStockage = storageUrl,
            DocumentKind = string.IsNullOrWhiteSpace(documentKind) ? "uploaded" : documentKind
        };
        _db.DemandeDocuments.Add(document);

        // Deliberately not touching demande.UpdatedAt here: that field is the deadline anchor
        // for the 10-working-day MISSING_DOCUMENTS/REJECTED window (see WorkingDays,
        // SubmitMissingDocuments, Appeal, MissingDocumentsExpiryService) and the processing-time
        // end timestamp for closed cases (see ReportCalculations.ComputeProcessingTime). Bumping
        // it on every upload — which always happens right before the applicant actually submits
        // — reset that clock to "now" each time, making the deadline unenforceable. The upload's
        // own timestamp already lives on DemandeDocument.UploadedAt.
        await _db.SaveChangesAsync();

        _logger.LogInformation("Document {FileName} ({SizeBytes} bytes, kind {Kind}) uploaded for demande {NumeroReference}",
            file.FileName, file.Length, document.DocumentKind, demande.NumeroReference);

        return Ok(new DocumentUploadResultDto(document.Id, document.FileName, document.CheminStockage, document.DocumentKind, document.UploadedAt));
    }

    [HttpGet("track/{numeroReference}")]
    [AllowAnonymous]
    [EnableRateLimiting("case-tracking")]
    public async Task<ActionResult<DemandeDetailDto>> GetByNumeroReference(string numeroReference)
    {
        var id = await _db.Demandes.Where(d => d.NumeroReference == numeroReference).Select(d => (Guid?)d.Id).FirstOrDefaultAsync();
        if (id is null)
        {
            _logger.LogWarning("Tracking lookup for unknown numéro de référence {NumeroReference}", numeroReference);
            return NotFound();
        }

        var detail = await LoadDetailAsync(id.Value);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet]
    [Authorize(Policy = "Permission.Demandes.View")]
    public async Task<ActionResult<List<DemandeSummaryDto>>> List(string? equipe, string? statutCode)
    {
        var query = _db.Demandes
            .Include(d => d.TypeService)
            .Include(d => d.Statut)
            .Include(d => d.Applicant)
            .Include(d => d.SoumisParApplicant)
            .Include(d => d.SoumisParUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(equipe)) query = query.Where(d => d.EquipeAssignee == equipe);
        if (!string.IsNullOrWhiteSpace(statutCode)) query = query.Where(d => d.Statut!.Code == statutCode);

        var demandes = await query
            .OrderBy(d => d.DateDepot)
            .Select(d => new DemandeSummaryDto(
                d.Id, d.NumeroReference, d.TypeService!.Code, d.TypeService.Libelle, d.TypeService.Categorie,
                d.Statut!.Code, d.Statut.Libelle, d.CanalDepot, d.NoteDocumentsManquantes, d.NoteRejet, d.EquipeAssignee, d.DateDepot, d.UpdatedAt,
                SoumisParNom(d), d.Applicant!.Nom, d.RendezVousBiometrieAt))
            .ToListAsync();

        _logger.LogInformation("Demandes list requested by {User} (equipe={Equipe}, statut={Statut}) returned {Count} row(s)",
            User.Identity?.Name ?? "unknown", equipe ?? "*", statutCode ?? "*", demandes.Count);

        return Ok(demandes);
    }

    // Applicant self-service: "my requests" — everything this Applicant is either the
    // beneficiary of (ApplicantId) or personally submitted (SoumisParApplicantId), so it also
    // covers ones they filed on behalf of someone else. Distinct from the staff-only List()
    // above (which sees every demande) and from GetByEmail on ApplicantsController (an
    // admin lookup by a different applicant's email, beneficiary-only, no self-service auth).
    [HttpGet("mine")]
    [Authorize(Roles = "Applicant")]
    public async Task<ActionResult<List<DemandeSummaryDto>>> ListMine()
    {
        var applicantId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var demandes = await _db.Demandes
            .Include(d => d.TypeService)
            .Include(d => d.Statut)
            .Include(d => d.Applicant)
            .Include(d => d.SoumisParApplicant)
            .Include(d => d.SoumisParUser)
            .Where(d => d.ApplicantId == applicantId || d.SoumisParApplicantId == applicantId)
            .OrderByDescending(d => d.DateDepot)
            .Select(d => new DemandeSummaryDto(
                d.Id, d.NumeroReference, d.TypeService!.Code, d.TypeService.Libelle, d.TypeService.Categorie,
                d.Statut!.Code, d.Statut.Libelle, d.CanalDepot, d.NoteDocumentsManquantes, d.NoteRejet, d.EquipeAssignee, d.DateDepot, d.UpdatedAt,
                SoumisParNom(d), d.Applicant!.Nom, d.RendezVousBiometrieAt))
            .ToListAsync();

        _logger.LogInformation("Applicant {ApplicantId} listed their own {Count} demande(s)", applicantId, demandes.Count);

        return Ok(demandes);
    }

    [HttpPost("{id:guid}/transition")]
    [Authorize(Policy = "Permission.Demandes.Transition")]
    public async Task<IActionResult> Transition(Guid id, TransitionDemandeDto dto)
    {
        var demande = await _db.Demandes.Include(d => d.Statut).Include(d => d.Applicant).FirstOrDefaultAsync(d => d.Id == id);
        if (demande is null)
        {
            _logger.LogWarning("Transition rejected: demande {DemandeId} not found", id);
            return NotFound();
        }

        if (!DemandeWorkflowRules.TryGetTransition(demande.Statut!.Code, dto.Action, out var nextStatutCode))
        {
            _logger.LogWarning("Transition rejected for demande {NumeroReference}: action {Action} is not valid from status {Statut}",
                demande.NumeroReference, dto.Action, demande.Statut.Code);
            return Problem($"L'action « {dto.Action} » n'est pas valide depuis le statut « {demande.Statut.Code} ».", statusCode: StatusCodes.Status409Conflict);
        }

        // A rejection reason is mandatory once a case has actually been looked at — the
        // applicant needs it to know whether (and how) to appeal. SUBMITTED is exempt: rejecting
        // straight off the bat (e.g. an obviously invalid submission) hasn't gone through a
        // review cycle yet, so there's nothing case-specific to explain. The dashboard already
        // disables the Reject button client-side until a note is typed (see DashboardPage.jsx);
        // this is the same rule enforced server-side.
        if (string.Equals(dto.Action, "reject", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(demande.Statut.Code, "SUBMITTED", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(dto.Details))
        {
            _logger.LogWarning("Transition rejected for demande {NumeroReference}: reject from {Statut} requires a reason", demande.NumeroReference, demande.Statut.Code);
            return ValidationProblem("Un motif de rejet est requis afin que le demandeur sache pourquoi, et s'il peut faire appel.");
        }

        // Passeport-only: staff must pick the biometric appointment date/time before the case can
        // move to WAITING_BIOMETRICS — the dashboard disables the button client-side until one is
        // chosen (see DashboardPage.jsx); this is the same rule enforced server-side.
        if (string.Equals(dto.Action, "waiting-biometrics", StringComparison.OrdinalIgnoreCase) && dto.RendezVousAt is null)
        {
            _logger.LogWarning("Transition rejected for demande {NumeroReference}: waiting-biometrics requires an appointment date/time", demande.NumeroReference);
            return ValidationProblem("Une date et une heure de rendez-vous sont requises pour passer ce dossier en attente de biométrie.");
        }

        var actorName = User.FindFirstValue("displayName") ?? User.Identity?.Name ?? "staff";

        // The dossier now belongs to whichever region the acting staff member is in. Reaching
        // this endpoint at all only requires the Demandes.Transition permission (any Role can
        // hold it, regardless of Region) — EquipeAssignee is derived from the real actor's own
        // Region rather than hardcoded, so a South-Region account with this permission still
        // correctly hands the dossier to Abuja, not Lagos.
        var actorRegion = Enum.Parse<Region>(User.FindFirstValue(ClaimTypes.Role)!);
        var nextEquipe = await GetRegionLabelAsync(actorRegion);

        if (string.Equals(dto.Action, "waiting-biometrics", StringComparison.OrdinalIgnoreCase))
        {
            demande.RendezVousBiometrieAt = dto.RendezVousAt;
        }

        await ApplyTransitionAsync(demande, nextStatutCode!, dto.Action, dto.Details, actorName, nextEquipe);

        if (string.Equals(dto.Action, "waiting-biometrics", StringComparison.OrdinalIgnoreCase) && dto.RendezVousAt is not null)
        {
            await SendBiometricsAppointmentEmailAsync(demande, dto.RendezVousAt.Value);
        }

        return NoContent();
    }

    // Applicant self-service: appeal a rejection. Anonymous, like tracking/document upload — the
    // Demande's Id is the only credential needed, same trust model as UploadDocument. Blocked
    // once the 10-working-day appeal window (from the moment the case became REJECTED) has
    // passed.
    [HttpPost("{id:guid}/appeal")]
    [AllowAnonymous]
    [EnableRateLimiting("case-tracking")]
    public async Task<IActionResult> Appeal(Guid id, AppealDemandeDto dto)
    {
        var demande = await _db.Demandes.Include(d => d.Statut).FirstOrDefaultAsync(d => d.Id == id);
        if (demande is null) return NotFound();

        if (!string.Equals(demande.Statut!.Code, "REJECTED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Appeal rejected for demande {NumeroReference}: current status is {Statut}, not REJECTED",
                demande.NumeroReference, demande.Statut.Code);
            return Problem("Un recours ne peut être déposé que pour une demande rejetée.", statusCode: StatusCodes.Status409Conflict);
        }

        var deadline = WorkingDays.AddWorkingDays(demande.UpdatedAt, WorkingDays.AppealAndDocumentsDeadlineDays);
        if (DateTime.UtcNow > deadline)
        {
            _logger.LogWarning("Appeal rejected for demande {NumeroReference}: deadline {Deadline} has passed", demande.NumeroReference, deadline);
            return Problem("Le délai de recours est dépassé.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!DemandeWorkflowRules.TryGetTransition(demande.Statut.Code, "appeal", out var nextStatutCode))
        {
            return Problem("Le recours n'est pas disponible pour cette demande.", statusCode: StatusCodes.Status409Conflict);
        }

        await ApplyTransitionAsync(demande, nextStatutCode!, "appeal", dto.Reason, "applicant (appeal)", equipeAssignee: null);

        return NoContent();
    }

    // Applicant self-service: confirm missing documents have been uploaded (via UploadDocument
    // above, called separately) and send the case back for review. Same anonymous trust model as
    // Appeal, and the same 10-working-day deadline, measured from the moment the case became
    // MISSING_DOCUMENTS.
    [HttpPost("{id:guid}/submit-missing-documents")]
    [AllowAnonymous]
    [EnableRateLimiting("case-tracking")]
    public async Task<IActionResult> SubmitMissingDocuments(Guid id, SubmitMissingDocumentsDto dto)
    {
        var demande = await _db.Demandes.Include(d => d.Statut).FirstOrDefaultAsync(d => d.Id == id);
        if (demande is null) return NotFound();

        if (!string.Equals(demande.Statut!.Code, "MISSING_DOCUMENTS", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Document submission rejected for demande {NumeroReference}: current status is {Statut}, not MISSING_DOCUMENTS",
                demande.NumeroReference, demande.Statut.Code);
            return Problem("Les documents ne peuvent être soumis que lorsque la demande est en attente de documents manquants.", statusCode: StatusCodes.Status409Conflict);
        }

        var deadline = WorkingDays.AddWorkingDays(demande.UpdatedAt, WorkingDays.AppealAndDocumentsDeadlineDays);
        if (DateTime.UtcNow > deadline)
        {
            _logger.LogWarning("Document submission rejected for demande {NumeroReference}: deadline {Deadline} has passed", demande.NumeroReference, deadline);
            return Problem("Le délai pour soumettre les documents manquants est dépassé.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!DemandeWorkflowRules.TryGetTransition(demande.Statut.Code, "submit-documents", out var nextStatutCode))
        {
            return Problem("La soumission de documents n'est pas disponible pour cette demande.", statusCode: StatusCodes.Status409Conflict);
        }

        await ApplyTransitionAsync(demande, nextStatutCode!, "submit-documents", dto.Details, "applicant (documents submitted)", equipeAssignee: null);

        return NoContent();
    }

    // Shared by every transition entry point (staff Transition, and the two applicant
    // self-service endpoints above): writes the audit row, moves the Demande to its new Statut,
    // and keeps the two status-scoped notes (missing-documents reason / rejection reason) in
    // sync with whichever of those statuses is actually current.
    private async Task ApplyTransitionAsync(Demande demande, string nextStatutCode, string action, string? details, string actorName, string? equipeAssignee)
    {
        var nextStatut = await _db.Statuts.FirstOrDefaultAsync(s => s.Code == nextStatutCode)
            ?? throw new InvalidOperationException($"Statut '{nextStatutCode}' is not seeded.");
        var previousStatutCode = demande.Statut!.Code;

        _db.DemandeHistoriques.Add(new DemandeHistorique
        {
            DemandeId = demande.Id,
            StatutOrigineId = demande.StatutId,
            StatutDestinationId = nextStatut.Id,
            ActorName = actorName,
            Details = details
        });

        demande.StatutId = nextStatut.Id;
        if (equipeAssignee is not null) demande.EquipeAssignee = equipeAssignee;
        demande.UpdatedAt = DateTime.UtcNow;
        demande.NoteDocumentsManquantes = nextStatutCode == "MISSING_DOCUMENTS" ? details : null;
        demande.NoteRejet = nextStatutCode == "REJECTED" ? details : null;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Demande {NumeroReference} transitioned {From} -> {To} (action {Action}) by {Actor}",
            demande.NumeroReference, previousStatutCode, nextStatutCode, action, actorName);
    }

    // Notifies the applicant of their biometric capture appointment the moment staff sets it
    // (see Transition, action "waiting-biometrics"). Bilingual FR/EN since the applicant's
    // preferred language isn't tracked server-side (only the frontend's own localStorage knows
    // it). A failed send never blocks the transition itself — see SmtpEmailService.
    private async Task SendBiometricsAppointmentEmailAsync(Demande demande, DateTime rendezVousAt)
    {
        var applicant = demande.Applicant!;
        var when = rendezVousAt.ToString("dd/MM/yyyy 'à' HH:mm");

        var subject = $"Rendez-vous biométrie / Biometrics appointment — {demande.NumeroReference}";
        var body =
            $"Bonjour {applicant.Nom},\n\n" +
            $"Votre rendez-vous à l'ambassade pour la capture biométrique a été fixé au {when}.\n" +
            "N'oubliez pas d'apporter les documents originaux utilisés lors de votre demande.\n\n" +
            $"Numéro de dossier : {demande.NumeroReference}\n\n" +
            "---\n\n" +
            $"Hello {applicant.Nom},\n\n" +
            $"Your appointment at the embassy for the biometrics has been booked for {when}.\n" +
            "Do not forget to bring the original documents you used for the application.\n\n" +
            $"Case number: {demande.NumeroReference}";

        await _emailService.SendAsync(applicant.Email, applicant.Nom, subject, body);
    }

    // RegionLookup.Valeur mirrors the Region enum's ordinal (North=0/Lagos, South=1/Abuja), so a
    // User's Region can be resolved to its human label ("Lagos"/"Abuja") without duplicating that
    // mapping as a second hardcoded table. LibelleFr and LibelleEn are identical for these two
    // rows (city names), so either column is a safe, language-agnostic choice.
    private async Task<string> GetRegionLabelAsync(Region region)
    {
        var label = await _db.RegionLookups
            .Where(r => r.Valeur == (int)region)
            .Select(r => r.LibelleFr)
            .FirstOrDefaultAsync();
        return label ?? region.ToString();
    }

    private async Task<string> GenerateNumeroReferenceAsync(TypeService typeService)
    {
        var year = DateTime.UtcNow.Year;
        // Racy under concurrent writes (read-then-write on a count) — acceptable for MVP volume;
        // revisit with a DB sequence if throughput grows. Scoped per type so each service's
        // numbering (and its prefix, see NumeroReferenceGenerator) starts from 1 independently.
        var count = await _db.Demandes.CountAsync(d => d.DateDepot.Year == year && d.TypeServiceId == typeService.Id);
        return NumeroReferenceGenerator.Format(year, typeService.Code, count + 1);
    }

    // A User submitting is always "on behalf of" someone by definition (Users are never their
    // own beneficiary), so its name is always shown. An Applicant submitter's name is only
    // surfaced when it differs from the beneficiary — i.e. they submitted for someone else.
    private static string? SoumisParNom(Demande d) =>
        d.SoumisParUser != null
            ? d.SoumisParUser.Nom
            : (d.SoumisParApplicant != null && d.SoumisParApplicant.Id != d.ApplicantId ? d.SoumisParApplicant.Nom : null);

    private async Task<DemandeDetailDto?> LoadDetailAsync(Guid id)
    {
        var demande = await _db.Demandes
            .Include(d => d.Applicant)
            .Include(d => d.SoumisParApplicant)
            .Include(d => d.SoumisParUser)
            .Include(d => d.TypeService)
            .Include(d => d.Statut)
            .Include(d => d.DemandeVisa)
            .Include(d => d.DemandeEtatCivil)
            .Include(d => d.DemandePasseport)
            .Include(d => d.Documents)
            .Include(d => d.Historique).ThenInclude(h => h.StatutOrigine)
            .Include(d => d.Historique).ThenInclude(h => h.StatutDestination)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (demande is null) return null;

        return new DemandeDetailDto(
            demande.Id,
            demande.NumeroReference,
            demande.Applicant!.Nom,
            demande.Applicant.Email,
            demande.Applicant.Telephone,
            demande.TypeService!.Code,
            demande.TypeService.Libelle,
            demande.TypeService.Categorie,
            demande.Statut!.Code,
            demande.Statut.Libelle,
            demande.CanalDepot,
            demande.NoteDocumentsManquantes,
            demande.NoteRejet,
            demande.EquipeAssignee,
            demande.DateDepot,
            demande.UpdatedAt,
            demande.DemandeVisa is null ? null : new DemandeVisaDto(
                demande.DemandeVisa.TypeVisa, demande.DemandeVisa.PaysDestination,
                demande.DemandeVisa.DateEntreePrevue, demande.DemandeVisa.DureeSejourJours),
            demande.DemandeEtatCivil is null ? null : new DemandeEtatCivilDto(
                demande.DemandeEtatCivil.TypeActe, demande.DemandeEtatCivil.DateEvenement, demande.DemandeEtatCivil.LieuEvenement),
            demande.DemandePasseport is null ? null : new DemandePasseportDto(
                demande.DemandePasseport.TypeDemande, demande.DemandePasseport.NumeroPasseportActuel, demande.DemandePasseport.DateExpirationActuelle),
            demande.Documents.OrderBy(doc => doc.UploadedAt)
                .Select(doc => new DocumentSummaryDto(doc.Id, doc.FileName, doc.CheminStockage, doc.DocumentKind, doc.ValideParAgent, doc.UploadedAt))
                .ToList(),
            demande.Historique.OrderBy(h => h.DateChangement)
                .Select(h => new HistoriqueDto(h.StatutOrigine?.Code, h.StatutDestination!.Code, h.ActorName, h.Details, h.DateChangement))
                .ToList(),
            SoumisParNom(demande),
            demande.RendezVousBiometrieAt
        );
    }

    private ActionResult ValidationProblem(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        return ValidationProblem(ModelState);
    }
}
