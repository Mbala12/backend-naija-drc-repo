namespace Consular.Shared.Entities;

// Append-only audit trail for admin/user-management mutations (AdminController, UsersController)
// — the counterpart to DemandeHistorique, which only covers demande status transitions.
public class AdminAuditLog
{
    public long Id { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Create" | "Update" | "Delete"
    public string EntityType { get; set; } = string.Empty; // e.g. "User", "TypeService", "Statut"
    public string EntityId { get; set; } = string.Empty; // stringified — entities use mixed key types (Guid/long)
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
