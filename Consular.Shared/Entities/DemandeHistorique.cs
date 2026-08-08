namespace Consular.Shared.Entities;

// Append-only audit trail. Every status change is timestamped and attributable.
public class DemandeHistorique
{
    public long Id { get; set; }
    public Guid DemandeId { get; set; }

    public Guid? StatutOrigineId { get; set; } // null for the first transition (submission)
    public Statut? StatutOrigine { get; set; }

    public Guid StatutDestinationId { get; set; }
    public Statut? StatutDestination { get; set; }

    public string? ActorName { get; set; } // staff member or "system" or "citizen"
    public string? Details { get; set; }
    public DateTime DateChangement { get; set; } = DateTime.UtcNow;
}
