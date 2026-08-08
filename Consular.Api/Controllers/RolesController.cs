using System.Security.Claims;
using Consular.Api.Data;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Consular.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Controllers;

// Roles are what administrators use to decide "what is a staff member allowed to do" — see
// PermissionAuthorizationHandler, which checks a User's Role.Permissions on every request. The
// Permission catalog itself is fixed (seeded once, see DemoDataSeeder) and only ever listed here,
// never created/edited/deleted — only which Permissions a Role has is admin-configurable.
// Gated the same way as UsersController (Users.Manage, independent of Region/Admin.* permissions)
// since assigning roles is part of the same "manage staff access" concern.
[ApiController]
[Route("api/v1")]
[Authorize(Policy = "Permission.Users.Manage")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<RolesController> _logger;

    public RolesController(AppDbContext db, ILogger<RolesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string CurrentActor => User.FindFirstValue("displayName") ?? User.Identity?.Name ?? "admin";

    [HttpGet("permissions")]
    public async Task<ActionResult<List<PermissionDto>>> GetPermissions()
    {
        var permissions = await _db.Permissions
            .OrderBy(p => p.Code)
            .Select(p => new PermissionDto(p.Id, p.Code, p.Label))
            .ToListAsync();

        return Ok(permissions);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var roles = await _db.Roles
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.Permissions.Select(p => p.Code).ToList(), r.Permissions.Select(p => p.Id).ToList()))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} role(s)", CurrentActor, roles.Count);
        return Ok(roles);
    }

    [HttpPost("roles")]
    public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleDto dto)
    {
        var nameTaken = await _db.Roles.AnyAsync(r => r.Name == dto.Name);
        if (nameTaken)
        {
            _logger.LogWarning("{Actor} attempted to create a role with a name already in use: {Name}", CurrentActor, dto.Name);
            ModelState.AddModelError(nameof(dto.Name), "Un rôle portant ce nom existe déjà.");
            return ValidationProblem(ModelState);
        }

        var permissions = await _db.Permissions.Where(p => dto.PermissionIds.Contains(p.Id)).ToListAsync();
        var role = new Role { Name = dto.Name, Description = dto.Description, Permissions = permissions };
        _db.Roles.Add(role);
        _db.LogAudit(CurrentActor, "Create", "Role", role.Id, $"name={dto.Name}, permissions={permissions.Count}");
        await _db.SaveChangesAsync();

        _logger.LogInformation("{Actor} created role {Name} with {Count} permission(s)", CurrentActor, dto.Name, permissions.Count);
        return CreatedAtAction(nameof(GetRoles), new RoleDto(role.Id, role.Name, role.Description, permissions.Select(p => p.Code).ToList(), permissions.Select(p => p.Id).ToList()));
    }

    [HttpPut("roles/{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, UpdateRoleDto dto)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing role {Id}", CurrentActor, id);
            return NotFound();
        }

        var permissions = await _db.Permissions.Where(p => dto.PermissionIds.Contains(p.Id)).ToListAsync();
        role.Name = dto.Name;
        role.Description = dto.Description;
        role.Permissions = permissions;

        _db.LogAudit(CurrentActor, "Update", "Role", id, $"name={dto.Name}, permissions={permissions.Count}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated role {Name} ({Count} permission(s))", CurrentActor, dto.Name, permissions.Count);
        return NoContent();
    }

    [HttpDelete("roles/{id:guid}")]
    public Task<IActionResult> DeleteRole(Guid id) => AdminDeleteHelper.DeleteAndHandleConflict(_db, _logger, CurrentActor, $"role {id}", "Role", id, async () =>
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return false;
        _db.Roles.Remove(role);
        return true;
    });
}
