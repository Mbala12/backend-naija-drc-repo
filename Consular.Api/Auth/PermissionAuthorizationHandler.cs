using System.Security.Claims;
using Consular.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Auth;

// Checks the caller's current Role/Permissions from the database on every request, rather than
// trusting a claim baked into the JWT at login time — JWTs here live 8 hours (Jwt:ExpiryMinutes),
// so a permission an admin just revoked would otherwise stay usable until the token expires.
// An Applicant's NameIdentifier never matches a Users row, so this correctly fails closed for
// them without any extra check.
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _db;

    public PermissionAuthorizationHandler(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var hasPermission = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Role!.Permissions.Any(p => p.Code == requirement.PermissionCode))
            .FirstOrDefaultAsync();

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
