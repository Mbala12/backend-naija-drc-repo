using System.Security.Claims;
using Consular.Api.Data;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Consular.Shared.Entities;
using Consular.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Controllers;

// Requires the Admin.ViewData permission just to reach the controller, but MUTATION (edit/delete)
// is further restricted to Admin.ManageData on top of that — every Http Put/Delete action below
// stacks its own [Authorize(Policy = "Permission.Admin.ManageData")], same pattern as
// UsersController, so a Role with only Admin.ViewData can still list every applicant, every type
// de service, every statut, every demande and its child records, but cannot change or remove any
// of it. Separate from the public/lookups endpoints (active-only, read-only) and from the
// demandes dashboard (transition workflow, gated by its own Demandes.View/Demandes.Transition
// permissions). User (staff/admin account) and Role management live in UsersController/
// RolesController, gated by Users.Manage instead.
//
// Types de service / Statuts "delete" is a soft-delete (Actif = false) — those rows are
// reference data other tables point to, and the schema already has an Actif flag for exactly
// this. Everything else here is a real delete; where the schema enforces a foreign key
// (e.g. an Applicant with existing demandes) that fails, DeleteAndHandleConflict turns the
// resulting DbUpdateException into a 409 instead of a raw 500.
//
// Every mutation here is logged at Information with the actor's identity, since this
// controller is the one place the whole database can be changed by hand — it's the closest
// thing this app has to an admin audit trail outside of DemandeHistoriques.
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "Permission.Admin.ViewData")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext db, ILogger<AdminController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string CurrentActor => User.FindFirstValue("displayName") ?? User.Identity?.Name ?? "unknown";

    // --- Applicants ----------------------------------------------------------------------------

    [HttpGet("applicants")]
    public async Task<ActionResult<List<ApplicantSummaryDto>>> GetApplicants()
    {
        var applicants = await _db.Applicants
            .OrderBy(a => a.Nom)
            .Select(a => new ApplicantSummaryDto(
                a.Id, a.Nom, a.Email, a.Telephone, a.Nationalite, a.CreatedAt,
                a.MotDePasseHash != null,
                _db.Demandes.Count(d => d.ApplicantId == a.Id)))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} applicant(s)", CurrentActor, applicants.Count);
        return Ok(applicants);
    }

    [HttpPut("applicants/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateApplicant(Guid id, UpdateApplicantAdminDto dto)
    {
        var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.Id == id);
        if (applicant is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing applicant {Id}", CurrentActor, id);
            return NotFound();
        }

        applicant.Nom = dto.Nom;
        applicant.Email = dto.Email;
        applicant.Telephone = dto.Telephone;
        applicant.Nationalite = dto.Nationalite;

        _db.LogAudit(CurrentActor, "Update", "Applicant", id, $"email={dto.Email}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated applicant {Id} ({Email})", CurrentActor, id, dto.Email);
        return NoContent();
    }

    [HttpDelete("applicants/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteApplicant(Guid id) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"applicant {id}", "Applicant", id, async () =>
    {
        var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.Id == id);
        if (applicant is null) return false;
        _db.Applicants.Remove(applicant);
        return true;
    });

    // --- Types de service (soft-delete via Actif) --------------------------------------------

    [HttpGet("types-service")]
    public async Task<ActionResult<List<TypeServiceAdminDto>>> GetTypesService()
    {
        var types = await _db.TypeServices
            .OrderBy(t => t.Libelle)
            .Select(t => new TypeServiceAdminDto(t.Id, t.Code, t.Libelle, t.Categorie, t.Description, t.Actif, t.MontantFrais))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} type(s) de service", CurrentActor, types.Count);
        return Ok(types);
    }

    [HttpPut("types-service/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateTypeService(Guid id, UpdateTypeServiceAdminDto dto)
    {
        var typeService = await _db.TypeServices.FirstOrDefaultAsync(t => t.Id == id);
        if (typeService is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing type de service {Id}", CurrentActor, id);
            return NotFound();
        }

        typeService.Libelle = dto.Libelle;
        typeService.Description = dto.Description;
        typeService.Actif = dto.Actif;
        typeService.MontantFrais = dto.MontantFrais;

        _db.LogAudit(CurrentActor, "Update", "TypeService", id, $"code={typeService.Code}, actif={dto.Actif}, frais={dto.MontantFrais}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated type de service {Code} (actif={Actif}, frais={Frais})", CurrentActor, typeService.Code, dto.Actif, dto.MontantFrais);
        return NoContent();
    }

    [HttpDelete("types-service/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> DeactivateTypeService(Guid id)
    {
        var typeService = await _db.TypeServices.FirstOrDefaultAsync(t => t.Id == id);
        if (typeService is null)
        {
            _logger.LogWarning("{Actor} attempted to deactivate missing type de service {Id}", CurrentActor, id);
            return NotFound();
        }

        typeService.Actif = false;
        _db.LogAudit(CurrentActor, "Update", "TypeService", id, $"code={typeService.Code}, deactivated");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} deactivated type de service {Code}", CurrentActor, typeService.Code);
        return NoContent();
    }

    // --- Statuts (soft-delete via Actif) ------------------------------------------------------

    [HttpGet("statuts")]
    public async Task<ActionResult<List<StatutAdminDto>>> GetStatuts()
    {
        var statuts = await _db.Statuts
            .OrderBy(s => s.Ordre)
            .Select(s => new StatutAdminDto(s.Id, s.Code, s.Libelle, s.Ordre, s.EstFinal, s.Actif))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} statut(s)", CurrentActor, statuts.Count);
        return Ok(statuts);
    }

    [HttpPut("statuts/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateStatut(Guid id, UpdateStatutAdminDto dto)
    {
        var statut = await _db.Statuts.FirstOrDefaultAsync(s => s.Id == id);
        if (statut is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing statut {Id}", CurrentActor, id);
            return NotFound();
        }

        statut.Libelle = dto.Libelle;
        statut.Ordre = dto.Ordre;
        statut.EstFinal = dto.EstFinal;
        statut.Actif = dto.Actif;

        _db.LogAudit(CurrentActor, "Update", "Statut", id, $"code={statut.Code}, actif={dto.Actif}, final={dto.EstFinal}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated statut {Code} (actif={Actif}, final={Final})", CurrentActor, statut.Code, dto.Actif, dto.EstFinal);
        return NoContent();
    }

    [HttpDelete("statuts/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> DeactivateStatut(Guid id)
    {
        var statut = await _db.Statuts.FirstOrDefaultAsync(s => s.Id == id);
        if (statut is null)
        {
            _logger.LogWarning("{Actor} attempted to deactivate missing statut {Id}", CurrentActor, id);
            return NotFound();
        }

        statut.Actif = false;
        _db.LogAudit(CurrentActor, "Update", "Statut", id, $"code={statut.Code}, deactivated");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} deactivated statut {Code}", CurrentActor, statut.Code);
        return NoContent();
    }

    // --- Appointment slot templates (real delete — see AdminDeleteHelper below; Actif stays
    // available via Update for a temporary disable that doesn't lose the row) ------------------
    // Unlike everything else in this controller, rows here don't originate from public
    // submission or seeding alone — admins actually create them, so this is the first real
    // [HttpPost] in this controller (shape copied from RolesController.CreateRole: uniqueness
    // pre-check -> ValidationProblem on conflict, else Add + LogAudit + SaveChanges). Capacity is
    // scoped per Region+Categorie — a Passeport slot and a Visa slot at the same region/day/time
    // are different resources, not one shared pool.

    [HttpGet("appointment-slot-templates")]
    public async Task<ActionResult<List<AppointmentSlotTemplateAdminDto>>> GetAppointmentSlotTemplates()
    {
        var templates = await _db.AppointmentSlotTemplates
            .OrderBy(t => t.Region).ThenBy(t => t.Categorie).ThenBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Select(t => new AppointmentSlotTemplateAdminDto(t.Id, (int)t.Region, t.Categorie, t.DayOfWeek, t.StartTime, t.CapaciteMax, t.Actif))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} appointment slot template(s)", CurrentActor, templates.Count);
        return Ok(templates);
    }

    [HttpPost("appointment-slot-templates")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<ActionResult<AppointmentSlotTemplateAdminDto>> CreateAppointmentSlotTemplate(CreateAppointmentSlotTemplateAdminDto dto)
    {
        var region = (Region)dto.Region;
        var duplicate = await _db.AppointmentSlotTemplates.AnyAsync(t =>
            t.Region == region && t.Categorie == dto.Categorie && t.DayOfWeek == dto.DayOfWeek && t.StartTime == dto.StartTime);
        if (duplicate)
        {
            _logger.LogWarning("{Actor} attempted to create a duplicate appointment slot template ({Region} {Categorie} {Day} {Time})",
                CurrentActor, region, dto.Categorie, dto.DayOfWeek, dto.StartTime);
            ModelState.AddModelError(nameof(dto.StartTime), "Un créneau existe déjà pour cette région/catégorie/jour/heure.");
            return ValidationProblem(ModelState);
        }

        var template = new AppointmentSlotTemplate { Region = region, Categorie = dto.Categorie, DayOfWeek = dto.DayOfWeek, StartTime = dto.StartTime, CapaciteMax = dto.CapaciteMax };
        _db.AppointmentSlotTemplates.Add(template);
        _db.LogAudit(CurrentActor, "Create", "AppointmentSlotTemplate", template.Id, $"region={region}, categorie={dto.Categorie}, day={dto.DayOfWeek}, time={dto.StartTime}, capacite={dto.CapaciteMax}");
        await _db.SaveChangesAsync();

        _logger.LogInformation("{Actor} created appointment slot template {Region} {Categorie} {Day} {Time} (capacite={Capacite})",
            CurrentActor, region, dto.Categorie, dto.DayOfWeek, dto.StartTime, dto.CapaciteMax);
        return CreatedAtAction(nameof(GetAppointmentSlotTemplates),
            new AppointmentSlotTemplateAdminDto(template.Id, dto.Region, template.Categorie, template.DayOfWeek, template.StartTime, template.CapaciteMax, template.Actif));
    }

    [HttpPut("appointment-slot-templates/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateAppointmentSlotTemplate(Guid id, UpdateAppointmentSlotTemplateAdminDto dto)
    {
        var template = await _db.AppointmentSlotTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing appointment slot template {Id}", CurrentActor, id);
            return NotFound();
        }

        template.CapaciteMax = dto.CapaciteMax;
        template.Actif = dto.Actif;

        _db.LogAudit(CurrentActor, "Update", "AppointmentSlotTemplate", id, $"capacite={dto.CapaciteMax}, actif={dto.Actif}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated appointment slot template {Id} (capacite={Capacite}, actif={Actif})", CurrentActor, id, dto.CapaciteMax, dto.Actif);
        return NoContent();
    }

    // A real delete (not soft-deactivate, unlike Statuts/TypeServices above) — nothing carries a
    // foreign key to this table (a booking just stores a literal region+datetime on Demande), so
    // there's no FK-conflict risk. Same DeleteAndHandleConflict shape as DeleteApplicant above.
    [HttpDelete("appointment-slot-templates/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteAppointmentSlotTemplate(Guid id) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor, $"appointment slot template {id}", "AppointmentSlotTemplate", id, async () =>
    {
        var template = await _db.AppointmentSlotTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template is null) return false;
        _db.AppointmentSlotTemplates.Remove(template);
        return true;
    });

    // --- Demandes (descriptive fields only — Statut changes go through /transition) ----------

    [HttpPut("demandes/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateDemande(Guid id, UpdateDemandeAdminDto dto)
    {
        var demande = await _db.Demandes.FirstOrDefaultAsync(d => d.Id == id);
        if (demande is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing demande {Id}", CurrentActor, id);
            return NotFound();
        }

        demande.CanalDepot = dto.CanalDepot;
        demande.NoteDocumentsManquantes = dto.NoteDocumentsManquantes;
        demande.EquipeAssignee = dto.EquipeAssignee;
        demande.UpdatedAt = DateTime.UtcNow;

        _db.LogAudit(CurrentActor, "Update", "Demande", id, $"numero={demande.NumeroReference}, equipe={dto.EquipeAssignee}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated demande {NumeroReference} (équipe={Equipe})", CurrentActor, demande.NumeroReference, dto.EquipeAssignee);
        return NoContent();
    }

    [HttpDelete("demandes/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteDemande(Guid id) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"demande {id}", "Demande", id, async () =>
    {
        var demande = await _db.Demandes.FirstOrDefaultAsync(d => d.Id == id);
        if (demande is null) return false;
        _logger.LogWarning("{Actor} is deleting demande {NumeroReference} — this cascades to its documents, historique and visa/état civil rows",
            CurrentActor, demande.NumeroReference);
        _db.Demandes.Remove(demande); // cascades to Documents/Historique/Visa/EtatCivil
        return true;
    });

    // --- Documents ----------------------------------------------------------------------------

    [HttpGet("documents")]
    public async Task<ActionResult<List<DocumentAdminDto>>> GetDocuments()
    {
        var documents = await (
            from d in _db.DemandeDocuments
            join dem in _db.Demandes on d.DemandeId equals dem.Id
            orderby d.UploadedAt descending
            select new DocumentAdminDto(d.Id, dem.NumeroReference, d.FileName, d.CheminStockage, d.DocumentKind, d.ValideParAgent, d.UploadedAt)
        ).ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} document(s)", CurrentActor, documents.Count);
        return Ok(documents);
    }

    [HttpPut("documents/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateDocument(Guid id, UpdateDocumentAdminDto dto)
    {
        var document = await _db.DemandeDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing document {Id}", CurrentActor, id);
            return NotFound();
        }

        document.DocumentKind = dto.DocumentKind;
        document.ValideParAgent = dto.ValideParAgent;

        _db.LogAudit(CurrentActor, "Update", "DemandeDocument", id, $"file={document.FileName}, valide={dto.ValideParAgent}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated document {FileName} (validé={Valide})", CurrentActor, document.FileName, dto.ValideParAgent);
        return NoContent();
    }

    [HttpDelete("documents/{id:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteDocument(Guid id) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"document {id}", "DemandeDocument", id, async () =>
    {
        var document = await _db.DemandeDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null) return false;
        _db.DemandeDocuments.Remove(document);
        return true;
    });

    // --- Historique ---------------------------------------------------------------------------

    [HttpGet("historique")]
    public async Task<ActionResult<List<HistoriqueAdminDto>>> GetHistorique()
    {
        var historique = await (
            from h in _db.DemandeHistoriques
            join dem in _db.Demandes on h.DemandeId equals dem.Id
            orderby h.DateChangement descending
            select new HistoriqueAdminDto(
                h.Id, dem.NumeroReference,
                h.StatutOrigine != null ? h.StatutOrigine.Code : null,
                h.StatutDestination!.Code,
                h.ActorName, h.Details, h.DateChangement)
        ).ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} historique entrie(s)", CurrentActor, historique.Count);
        return Ok(historique);
    }

    [HttpPut("historique/{id:long}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateHistorique(long id, UpdateHistoriqueAdminDto dto)
    {
        var entry = await _db.DemandeHistoriques.FirstOrDefaultAsync(h => h.Id == id);
        if (entry is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing historique entry {Id}", CurrentActor, id);
            return NotFound();
        }

        entry.ActorName = dto.ActorName;
        entry.Details = dto.Details;

        // Logged to AdminAuditLog too (not just ILogger) since this is itself an audit-trail
        // mutation — it needs to survive in the same queryable record as everything else here.
        _db.LogAudit(CurrentActor, "Update", "DemandeHistorique", id, "edited an append-only audit-trail entry");
        await _db.SaveChangesAsync();
        // Warning, not Information: this table is meant to be an append-only audit trail —
        // editing a past entry is a deliberate exception worth standing out in the logs.
        _logger.LogWarning("{Actor} edited audit-trail entry {Id} — this weakens its integrity as an immutable record", CurrentActor, id);
        return NoContent();
    }

    [HttpDelete("historique/{id:long}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteHistorique(long id) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"historique entry {id}", "DemandeHistorique", id, async () =>
    {
        var entry = await _db.DemandeHistoriques.FirstOrDefaultAsync(h => h.Id == id);
        if (entry is null) return false;
        _logger.LogWarning("{Actor} is deleting audit-trail entry {Id} — this weakens its integrity as an immutable record", CurrentActor, id);
        _db.DemandeHistoriques.Remove(entry);
        return true;
    });

    // --- Visas ----------------------------------------------------------------------------------

    [HttpGet("visas")]
    public async Task<ActionResult<List<VisaAdminDto>>> GetVisas()
    {
        var visas = await _db.DemandeVisas
            .Include(v => v.Demande)
            .OrderByDescending(v => v.Demande!.DateDepot)
            .Select(v => new VisaAdminDto(v.DemandeId, v.Demande!.NumeroReference, v.TypeVisa, v.PaysDestination, v.DateEntreePrevue, v.DureeSejourJours))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} visa(s)", CurrentActor, visas.Count);
        return Ok(visas);
    }

    [HttpPut("visas/{demandeId:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateVisa(Guid demandeId, UpdateVisaAdminDto dto)
    {
        var visa = await _db.DemandeVisas.FirstOrDefaultAsync(v => v.DemandeId == demandeId);
        if (visa is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing visa for demande {DemandeId}", CurrentActor, demandeId);
            return NotFound();
        }

        visa.TypeVisa = dto.TypeVisa;
        visa.PaysDestination = dto.PaysDestination;
        visa.DateEntreePrevue = dto.DateEntreePrevue;
        visa.DureeSejourJours = dto.DureeSejourJours;

        _db.LogAudit(CurrentActor, "Update", "DemandeVisa", demandeId, $"type={dto.TypeVisa}, pays={dto.PaysDestination}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated visa for demande {DemandeId} ({TypeVisa} -> {Pays})", CurrentActor, demandeId, dto.TypeVisa, dto.PaysDestination);
        return NoContent();
    }

    [HttpDelete("visas/{demandeId:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteVisa(Guid demandeId) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"visa for demande {demandeId}", "DemandeVisa", demandeId, async () =>
    {
        var visa = await _db.DemandeVisas.FirstOrDefaultAsync(v => v.DemandeId == demandeId);
        if (visa is null) return false;
        _logger.LogWarning("{Actor} is deleting the visa details for demande {DemandeId} — the demande itself is untouched but will report no visa data",
            CurrentActor, demandeId);
        _db.DemandeVisas.Remove(visa);
        return true;
    });

    // --- Passeport ------------------------------------------------------------------------------

    [HttpGet("passeports")]
    public async Task<ActionResult<List<PasseportAdminDto>>> GetPasseports()
    {
        var passeports = await _db.DemandePasseports
            .Include(p => p.Demande)
            .OrderByDescending(p => p.Demande!.DateDepot)
            .Select(p => new PasseportAdminDto(p.DemandeId, p.Demande!.NumeroReference, p.TypeDemande, p.NumeroPasseportActuel, p.DateExpirationActuelle))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} passeport record(s)", CurrentActor, passeports.Count);
        return Ok(passeports);
    }

    [HttpPut("passeports/{demandeId:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdatePasseport(Guid demandeId, UpdatePasseportAdminDto dto)
    {
        var passeport = await _db.DemandePasseports.FirstOrDefaultAsync(p => p.DemandeId == demandeId);
        if (passeport is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing passeport record for demande {DemandeId}", CurrentActor, demandeId);
            return NotFound();
        }

        passeport.TypeDemande = dto.TypeDemande;
        passeport.NumeroPasseportActuel = dto.NumeroPasseportActuel;
        passeport.DateExpirationActuelle = dto.DateExpirationActuelle;

        _db.LogAudit(CurrentActor, "Update", "DemandePasseport", demandeId, $"type={dto.TypeDemande}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated passeport record for demande {DemandeId} ({TypeDemande})", CurrentActor, demandeId, dto.TypeDemande);
        return NoContent();
    }

    [HttpDelete("passeports/{demandeId:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeletePasseport(Guid demandeId) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"passeport record for demande {demandeId}", "DemandePasseport", demandeId, async () =>
    {
        var passeport = await _db.DemandePasseports.FirstOrDefaultAsync(p => p.DemandeId == demandeId);
        if (passeport is null) return false;
        _logger.LogWarning("{Actor} is deleting the passeport details for demande {DemandeId} — the demande itself is untouched but will report no passeport data",
            CurrentActor, demandeId);
        _db.DemandePasseports.Remove(passeport);
        return true;
    });

    // --- État civil -----------------------------------------------------------------------------

    [HttpGet("etat-civil")]
    public async Task<ActionResult<List<EtatCivilAdminDto>>> GetEtatCivil()
    {
        var etatCivil = await _db.DemandeEtatCivils
            .Include(v => v.Demande)
            .OrderByDescending(v => v.Demande!.DateDepot)
            .Select(v => new EtatCivilAdminDto(v.DemandeId, v.Demande!.NumeroReference, v.TypeActe, v.DateEvenement, v.LieuEvenement))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} état civil record(s)", CurrentActor, etatCivil.Count);
        return Ok(etatCivil);
    }

    [HttpPut("etat-civil/{demandeId:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public async Task<IActionResult> UpdateEtatCivil(Guid demandeId, UpdateEtatCivilAdminDto dto)
    {
        var etatCivil = await _db.DemandeEtatCivils.FirstOrDefaultAsync(v => v.DemandeId == demandeId);
        if (etatCivil is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing état civil record for demande {DemandeId}", CurrentActor, demandeId);
            return NotFound();
        }

        etatCivil.TypeActe = dto.TypeActe;
        etatCivil.DateEvenement = dto.DateEvenement;
        etatCivil.LieuEvenement = dto.LieuEvenement;

        _db.LogAudit(CurrentActor, "Update", "DemandeEtatCivil", demandeId, $"type={dto.TypeActe}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated état civil record for demande {DemandeId} ({TypeActe})", CurrentActor, demandeId, dto.TypeActe);
        return NoContent();
    }

    [HttpDelete("etat-civil/{demandeId:guid}")]
    [Authorize(Policy = "Permission.Admin.ManageData")]
    public Task<IActionResult> DeleteEtatCivil(Guid demandeId) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor,$"état civil record for demande {demandeId}", "DemandeEtatCivil", demandeId, async () =>
    {
        var etatCivil = await _db.DemandeEtatCivils.FirstOrDefaultAsync(v => v.DemandeId == demandeId);
        if (etatCivil is null) return false;
        _logger.LogWarning("{Actor} is deleting the état civil details for demande {DemandeId} — the demande itself is untouched but will report no état civil data",
            CurrentActor, demandeId);
        _db.DemandeEtatCivils.Remove(etatCivil);
        return true;
    });

    // --- Audit log (admin/user-management mutations — see AdminAuditLogExtensions) -----------

    [HttpGet("audit-log")]
    public async Task<ActionResult<List<AuditLogAdminDto>>> GetAuditLog()
    {
        var entries = await _db.AdminAuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AuditLogAdminDto(a.Id, a.ActorName, a.Action, a.EntityType, a.EntityId, a.Details, a.CreatedAt))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} audit-log entrie(s)", CurrentActor, entries.Count);
        return Ok(entries);
    }

}
