using System.ComponentModel.DataAnnotations;
using Consular.Shared.Enums;

namespace Consular.Api.Dtos;

public record UserDto(Guid Id, string Nom, string Email, Region Region, Guid RoleId, string RoleName, DateTime CreatedAt);

public record CreateUserDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Nom,
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email)] string Email,
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 6, ErrorMessage = ValidationMessages.StringLength)] string Password,
    [Required(ErrorMessage = ValidationMessages.Required)] Region Region,
    [Required(ErrorMessage = ValidationMessages.Required)] Guid RoleId
);

public record UpdateUserDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(200, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Nom,
    [Required(ErrorMessage = ValidationMessages.Required), EmailAddress(ErrorMessage = ValidationMessages.Email)] string Email,
    [Required(ErrorMessage = ValidationMessages.Required)] Region Region,
    [Required(ErrorMessage = ValidationMessages.Required)] Guid RoleId
);
