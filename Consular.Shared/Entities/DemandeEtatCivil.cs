namespace Consular.Shared.Entities;

// One-to-zero-or-one extension of Demande for TypeService.Categorie == EtatCivil.
public class DemandeEtatCivil
{
    public Guid DemandeId { get; set; } // shared primary key / FK to Demande
    public Demande? Demande { get; set; }

    public string TypeActe { get; set; } = string.Empty; // "naissance", "mariage", "deces"
    public DateOnly DateEvenement { get; set; }
    public string LieuEvenement { get; set; } = string.Empty;
}
