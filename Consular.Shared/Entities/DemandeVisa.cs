namespace Consular.Shared.Entities;

// One-to-zero-or-one extension of Demande for TypeService.Categorie == Visa.
public class DemandeVisa
{
    public Guid DemandeId { get; set; } // shared primary key / FK to Demande
    public Demande? Demande { get; set; }

    public string TypeVisa { get; set; } = string.Empty;
    public string PaysDestination { get; set; } = string.Empty;
    public DateOnly DateEntreePrevue { get; set; }
    public int DureeSejourJours { get; set; }
}
