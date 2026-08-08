using Consular.Shared.Enums;

namespace Consular.Shared.Entities;

// A staff/admin account — distinct from Applicant (see Applicant.cs). A User always has
// credentials (no beneficiary-only concept here) and a Region, which continues to mean "which
// team" (Lagos/Abuja) a dossier gets assigned to — it no longer has anything to do with what the
// User is allowed to do. Authorization is entirely decided by Role (see Role.cs/Permission.cs
// and PermissionAuthorizationHandler): a South Role.Permissions-heavy account and a North
// read-only account are both valid combinations.
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public Region Region { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
