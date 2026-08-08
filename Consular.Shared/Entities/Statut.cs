namespace Consular.Shared.Entities;

// Shared workflow status lookup, reused by Demande.StatutId and both DemandeHistorique FKs
// (StatutOrigineId/StatutDestinationId) so the whole state machine reads from one table.
public class Statut
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool EstFinal { get; set; }
    public bool Actif { get; set; } = true;
}
