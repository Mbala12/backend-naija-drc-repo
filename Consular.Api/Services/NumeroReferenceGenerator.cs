namespace Consular.Api.Services;

// Formats a demande's public tracking number. Kept as a pure, DB-free function so the
// fiddly string-formatting part (prefix + year + padded sequence) is unit-testable —
// the actual per-type, per-year counting still happens in DemandesController, since it
// needs a database query.
public static class NumeroReferenceGenerator
{
    // Every service type gets a fixed 3-digit prefix so the tracking number itself hints
    // at what kind of request it is. Unmapped/future types fall back to "000" — there's no
    // admin "create type de service" endpoint today, so this is a hardcoded map rather than
    // a TypeService column; add an entry here (and reseed) when a new service type is added.
    private static readonly Dictionary<string, string> PrefixByTypeServiceCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACTE_NAISSANCE"] = "111",
        ["ACTE_MARIAGE"] = "112",
        ["ACTE_DECES"] = "113",
        ["PASSPORT_RENEWAL"] = "222",
        ["VISA_TOURISTIQUE"] = "331",
        ["VISA_AFFAIRES"] = "332",
        ["VISA_ETUDIANT"] = "333",
        ["VISA_TRANSIT"] = "334",
        ["VISA_TRAVAIL"] = "335",
        ["VISA_DIPLOMATIQUE"] = "336"
    };

    public static string Format(int year, string typeServiceCode, int sequenceForType)
    {
        var prefix = PrefixByTypeServiceCode.TryGetValue(typeServiceCode, out var mapped) ? mapped : "000";
        return $"DEM-{year}-{prefix}{sequenceForType:D3}";
    }
}
