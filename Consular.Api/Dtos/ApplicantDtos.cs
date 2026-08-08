namespace Consular.Api.Dtos;

public record ApplicantDto(
    Guid Id,
    string Nom,
    string Email,
    string Telephone,
    string Nationalite,
    DateTime CreatedAt,
    List<DemandeSummaryDto> Demandes
);

// HasCompte distinguishes a real registered account (MotDePasseHash set) from a
// beneficiary-only record created when someone submitted a demande on their behalf.
public record ApplicantSummaryDto(
    Guid Id,
    string Nom,
    string Email,
    string Telephone,
    string Nationalite,
    DateTime CreatedAt,
    bool HasCompte,
    int DemandesCount
);
