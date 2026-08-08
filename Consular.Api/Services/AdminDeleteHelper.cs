using Consular.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Services;

// Shared by AdminController and RolesController: `remove` stages the deletion and returns false
// if the row was already gone (-> 404). A foreign key still pointing at the row (e.g. a Role
// still assigned to a User) surfaces as a DbUpdateException from SaveChangesAsync, turned into a
// 409 instead of a raw 500.
public static class AdminDeleteHelper
{
    public static async Task<IActionResult> DeleteAndHandleConflict(
        AppDbContext db, ILogger logger, string actor,
        string description, string entityType, object entityId, Func<Task<bool>> remove)
    {
        try
        {
            if (!await remove())
            {
                logger.LogWarning("{Actor} attempted to delete missing {Description}", actor, description);
                return new NotFoundResult();
            }

            db.LogAudit(actor, "Delete", entityType, entityId, description);
            await db.SaveChangesAsync();
            logger.LogInformation("{Actor} deleted {Description}", actor, description);
            return new NoContentResult();
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "{Actor} could not delete {Description}: referenced by other data", actor, description);
            return new ObjectResult(new ProblemDetails
            {
                Detail = "This record is referenced by other data and cannot be deleted.",
                Status = StatusCodes.Status409Conflict
            })
            { StatusCode = StatusCodes.Status409Conflict };
        }
    }
}
