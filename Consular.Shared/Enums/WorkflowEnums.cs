namespace Consular.Shared.Enums;

// Region determines routing: dossiers from "the south" transit through the liaison team first.
public enum Region
{
    North = 0,
    South = 1
}

// Determines which extension table (if any) a Demande of this TypeService gets.
public enum TypeServiceCategorie
{
    Generique = 0,
    Visa = 1,
    EtatCivil = 2,
    Passeport = 3
}
