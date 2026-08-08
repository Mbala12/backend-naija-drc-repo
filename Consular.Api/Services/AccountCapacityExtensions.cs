using Consular.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Services;

// Demo-scope hard cap on total accounts (Applicants + Users combined) — checked at every place a
// new one gets created (AuthController.Register, DemandesController.Create's beneficiary
// auto-create, UsersController.CreateUser). Doesn't call SaveChangesAsync itself — same
// "caller controls the unit of work" convention as AdminAuditLogExtensions.LogAudit. An
// application-level count check (not a DB constraint) is a known race under concurrent writes,
// same accepted tradeoff already used elsewhere in this codebase for demo-scale volume (see
// NumeroReferenceGenerator).
public static class AccountCapacityExtensions
{
    public const int MaxAccounts = 25;

    public static async Task<bool> HasCapacityForNewAccountAsync(this AppDbContext db)
    {
        var total = await db.Applicants.CountAsync() + await db.Users.CountAsync();
        return total < MaxAccounts;
    }
}
