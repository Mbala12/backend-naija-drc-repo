using Consular.Api.Data;
using Consular.Shared.Entities;

namespace Consular.Api.Services;

// Shared by AdminController and UsersController: appends one row per admin mutation, mirroring
// how DemandeHistorique captures demande status changes. Doesn't call SaveChangesAsync itself —
// callers add it to the same unit of work as the actual mutation, so a failed save rolls back
// the audit row along with it.
public static class AdminAuditLogExtensions
{
    public static void LogAudit(this AppDbContext db, string actorName, string action, string entityType, object entityId, string? details = null)
    {
        db.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            ActorName = actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString() ?? string.Empty,
            Details = details
        });
    }
}
