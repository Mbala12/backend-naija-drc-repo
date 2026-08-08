using Consular.Shared.Enums;

namespace Consular.Shared.Entities;

// Lookup table for the service catalog (visa types, état civil acts, etc.). Categorie determines
// which extension table (DemandeVisa / DemandeEtatCivil / none) a Demande of this type gets.
public class TypeService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public TypeServiceCategorie Categorie { get; set; }
    public string? Description { get; set; }
    public bool Actif { get; set; } = true;

    // Consular fee for this service, shown in the admin Types de service tab.
    public decimal MontantFrais { get; set; }

    // Shown to applicants before they submit, so they can gather everything in advance rather
    // than finding out about missing paperwork only after review starts. Stored per-language
    // (mirroring RegionLookup's LibelleFr/LibelleEn) rather than run through translateCode,
    // since these are free-text checklists, not fixed enum-backed codes.
    public List<string> DocumentsRequisFr { get; set; } = new();
    public List<string> DocumentsRequisEn { get; set; } = new();

    public ICollection<Demande> Demandes { get; set; } = new List<Demande>();
}
