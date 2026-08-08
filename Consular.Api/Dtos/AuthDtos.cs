using System.ComponentModel.DataAnnotations;
using Consular.Shared.Enums;

namespace Consular.Api.Dtos;

// Public self-registration — creates an Applicant only. No Region field: applicants carry no
// role, unlike Users (staff/admin), who can only be created by an existing admin.
public record RegisterDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Nom,
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email)] string Email,
    [Required(ErrorMessage = ValidationMessages.Required), Phone(ErrorMessage = ValidationMessages.Phone)] string Telephone,
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Nationalite,
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 6, ErrorMessage = ValidationMessages.StringLength)] string Password
);

public record LoginDto(
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email)] string Email,
    [Required(ErrorMessage = ValidationMessages.Required)] string Password
);

// Role is "Applicant" for a self-service account, or the User's Region ("North"/"South") for
// staff — this is what [Authorize(Roles=...)] still checks for the handful of Region/account-type
// gates that remain (e.g. Applicant-only endpoints). Permissions is the User's actual authority
// (see Role.Permissions/PermissionAuthorizationHandler) — always empty for an Applicant, which
// has no Role. Region is only ever populated for a User response; an Applicant has neither.
public record AuthResponseDto(string Token, string DisplayName, string Role, string Email, string Nom, Region? Region, List<string> Permissions, string? Telephone);
