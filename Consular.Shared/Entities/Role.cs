namespace Consular.Shared.Entities;

// Admin-editable: administrators create/edit Roles and assign them a subset of the fixed
// Permission catalog (see Permission.cs), then assign a Role to each staff User (User.RoleId) —
// this is what now decides authorization (PermissionAuthorizationHandler), decoupled from
// User.Region, which continues to mean "which team" (Lagos/Abuja), not "what am I allowed to do".
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
