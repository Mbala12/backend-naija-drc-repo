namespace Consular.Shared.Entities;

// Fixed catalog (one row per distinct authorization gate in the app) — seeded once, never
// admin-created or deleted. What's admin-configurable is which Permissions a Role has, not the
// Permission list itself. See Role.cs.
public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
