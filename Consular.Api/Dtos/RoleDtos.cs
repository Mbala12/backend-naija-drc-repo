using System.ComponentModel.DataAnnotations;

namespace Consular.Api.Dtos;

// Permission is a fixed catalog (see Consular.Shared.Entities.Permission) — listed here only for
// the admin UI to render as checkboxes, never created/edited/deleted through the API.
public record PermissionDto(Guid Id, string Code, string Label);

// PermissionCodes is for read-only display; PermissionIds is what the edit form actually submits
// back via UpdateRoleDto (CreateRoleDto/UpdateRoleDto take ids, not codes, since Code isn't the
// Permission table's key).
public record RoleDto(Guid Id, string Name, string? Description, List<string> PermissionCodes, List<Guid> PermissionIds);

public record CreateRoleDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Name,
    string? Description,
    List<Guid> PermissionIds
);

public record UpdateRoleDto(
    [Required(ErrorMessage = ValidationMessages.Required), StringLength(100, MinimumLength = 2, ErrorMessage = ValidationMessages.StringLength)] string Name,
    string? Description,
    List<Guid> PermissionIds
);
