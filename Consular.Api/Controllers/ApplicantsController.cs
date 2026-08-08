using Consular.Api.Data;
using Consular.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Controllers;

// Lookup only — accounts are created via POST /api/auth/register now that Applicant can carry
// credentials, so this no longer exposes an anonymous write path.
[ApiController]
[Route("api/v1/applicants")]
[Authorize(Policy = "Permission.Applicants.Lookup")]
public class ApplicantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApplicantsController> _logger;

    public ApplicantsController(AppDbContext db, ILogger<ApplicantsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("{email}")]
    public async Task<ActionResult<ApplicantDto>> GetByEmail(string email)
    {
        var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.Email == email);
        if (applicant is null)
        {
            _logger.LogWarning("Applicant lookup by {User} found no match for {Email}", User.Identity?.Name ?? "unknown", email);
            return NotFound();
        }

        var demandes = await _db.Demandes
            .Where(d => d.ApplicantId == applicant.Id)
            .Include(d => d.TypeService)
            .Include(d => d.Statut)
            .OrderByDescending(d => d.DateDepot)
            .Select(d => new DemandeSummaryDto(
                d.Id, d.NumeroReference, d.TypeService!.Code, d.TypeService.Libelle, d.TypeService.Categorie,
                d.Statut!.Code, d.Statut.Libelle, d.CanalDepot, d.NoteDocumentsManquantes, d.NoteRejet, d.EquipeAssignee, d.DateDepot, d.UpdatedAt, null, applicant.Nom, d.RendezVousBiometrieAt))
            .ToListAsync();

        _logger.LogInformation("Applicant lookup by {User} for {Email} returned {DemandeCount} demande(s)",
            User.Identity?.Name ?? "unknown", email, demandes.Count);

        return Ok(new ApplicantDto(applicant.Id, applicant.Nom, applicant.Email, applicant.Telephone, applicant.Nationalite, applicant.CreatedAt, demandes));
    }
}
