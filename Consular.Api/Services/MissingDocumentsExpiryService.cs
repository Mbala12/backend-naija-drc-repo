using Consular.Api.Data;
using Consular.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Services;

// Background sweep for the MISSING_DOCUMENTS deadline (see WorkingDays.AppealAndDocumentsDeadlineDays).
// Until now that 10-working-day window was only enforced reactively — DemandesController.
// SubmitMissingDocuments rejects a LATE submission, but a demande the applicant simply never
// acted on just sat in MISSING_DOCUMENTS forever. This periodically finds anything past its
// deadline and auto-rejects it, the same way a staff "reject" would (a Historique row + NoteRejet),
// just with ActorName "system" instead of a real person. REJECTED itself has its own 10-working-day
// appeal window, but that one isn't auto-expired here — there's nothing to transition it TO;
// letting the deadline pass just makes DemandesController.Appeal's existing reactive check start
// rejecting appeal attempts, which is already in place.
public class MissingDocumentsExpiryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<MissingDocumentsExpiryService> _logger;

    public MissingDocumentsExpiryService(IServiceProvider services, ILogger<MissingDocumentsExpiryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOverdueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Missing-documents expiry sweep failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Normal on shutdown.
            }
        }
    }

    private async Task ExpireOverdueAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rejectedStatut = await db.Statuts.FirstOrDefaultAsync(s => s.Code == "REJECTED", ct);
        if (rejectedStatut is null) return; // Not seeded yet — shouldn't happen past startup, but no-op is safe.

        var overdue = await db.Demandes
            .Include(d => d.Statut)
            .Where(d => d.Statut!.Code == "MISSING_DOCUMENTS")
            .ToListAsync(ct);

        var expiredCount = 0;
        foreach (var demande in overdue)
        {
            var deadline = WorkingDays.AddWorkingDays(demande.UpdatedAt, WorkingDays.AppealAndDocumentsDeadlineDays);
            if (DateTime.UtcNow <= deadline) continue;

            db.DemandeHistoriques.Add(new DemandeHistorique
            {
                DemandeId = demande.Id,
                StatutOrigineId = demande.StatutId,
                StatutDestinationId = rejectedStatut.Id,
                ActorName = "system",
                Details = "Documents were not submitted within the 10 working day deadline."
            });

            demande.StatutId = rejectedStatut.Id;
            demande.NoteRejet = "Documents were not submitted within the 10 working day deadline.";
            demande.NoteDocumentsManquantes = null;
            demande.UpdatedAt = DateTime.UtcNow;
            expiredCount++;

            _logger.LogWarning("Demande {NumeroReference} auto-rejected: missing-documents deadline ({Deadline}) passed",
                demande.NumeroReference, deadline);
        }

        if (expiredCount > 0) await db.SaveChangesAsync(ct);
    }
}
