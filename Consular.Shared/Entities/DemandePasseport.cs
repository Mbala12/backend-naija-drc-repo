namespace Consular.Shared.Entities;

// One-to-zero-or-one extension of Demande for TypeService.Categorie == Passeport.
public class DemandePasseport
{
    public Guid DemandeId { get; set; } // shared primary key / FK to Demande
    public Demande? Demande { get; set; }

    public string TypeDemande { get; set; } = string.Empty; // "Nouveau", "Renouvellement", "Perte/Vol"
    public string? NumeroPasseportActuel { get; set; } // absent for a first-time "Nouveau" request
    public DateOnly? DateExpirationActuelle { get; set; }
}
