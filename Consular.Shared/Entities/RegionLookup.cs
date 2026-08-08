namespace Consular.Shared.Entities;

public class RegionLookup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public int Valeur { get; set; }
    public string LibelleFr { get; set; } = string.Empty;
    public string LibelleEn { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;
}
