using System.Security.Claims;
using Consular.Api.Data;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserEntity = Consular.Shared.Entities.User;

namespace Consular.Api.Controllers;

// Managing Users (staff/admin accounts) is gated purely by the Users.Manage permission,
// independent of Region — deliberately a separate controller from AdminController (which
// requires Admin.ViewData/Admin.ManageData), since a Role without any Admin.* permission can
// still hold Users.Manage and reach these endpoints (and RolesController's).
//
// Aliased to UserEntity throughout: ControllerBase already exposes a `User` property (the
// ClaimsPrincipal) which would otherwise shadow the `Consular.Shared.Entities.User` type name.
[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "Permission.Users.Manage")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<UsersController> _logger;
    private readonly PasswordHasher<UserEntity> _hasher = new();

    public UsersController(AppDbContext db, ILogger<UsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string CurrentActor => User.FindFirstValue("displayName") ?? User.Identity?.Name ?? "admin";

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await _db.Users
            .Include(u => u.Role)
            .OrderBy(u => u.Nom)
            .Select(u => new UserDto(u.Id, u.Nom, u.Email, u.Region, u.RoleId, u.Role!.Name, u.CreatedAt))
            .ToListAsync();

        _logger.LogDebug("{Actor} listed {Count} user(s)", CurrentActor, users.Count);
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto dto)
    {
        var emailTaken = await _db.Users.AnyAsync(u => u.Email == dto.Email) || await _db.Applicants.AnyAsync(a => a.Email == dto.Email);
        if (emailTaken)
        {
            _logger.LogWarning("{Actor} attempted to create a user for an email already in use: {Email}", CurrentActor, dto.Email);
            ModelState.AddModelError(nameof(dto.Email), "Un compte existe déjà avec cette adresse email.");
            return ValidationProblem(ModelState);
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
        if (role is null)
        {
            _logger.LogWarning("{Actor} attempted to create a user with an unknown role {RoleId}", CurrentActor, dto.RoleId);
            ModelState.AddModelError(nameof(dto.RoleId), "Ce rôle n'existe pas.");
            return ValidationProblem(ModelState);
        }

        if (!await _db.HasCapacityForNewAccountAsync())
        {
            _logger.LogWarning("{Actor} attempted to create a user but the account limit has been reached", CurrentActor);
            ModelState.AddModelError(string.Empty, $"La limite de {AccountCapacityExtensions.MaxAccounts} comptes pour cette démo a été atteinte. Supprimez un postulant ou un utilisateur existant avant d'en ajouter un autre.");
            return ValidationProblem(ModelState);
        }

        var user = new UserEntity { Nom = dto.Nom, Email = dto.Email, Region = dto.Region, RoleId = dto.RoleId };
        user.MotDePasseHash = _hasher.HashPassword(user, dto.Password);
        _db.Users.Add(user);
        _db.LogAudit(CurrentActor, "Create", "User", user.Id, $"email={dto.Email}, region={dto.Region}, role={role.Name}");
        await _db.SaveChangesAsync();

        _logger.LogInformation("{Actor} created user {Email} (region={Region}, role={Role})", CurrentActor, dto.Email, dto.Region, role.Name);
        return CreatedAtAction(nameof(GetUsers), new UserDto(user.Id, user.Nom, user.Email, user.Region, user.RoleId, role.Name, user.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            _logger.LogWarning("{Actor} attempted to update missing user {Id}", CurrentActor, id);
            return NotFound();
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
        if (role is null)
        {
            _logger.LogWarning("{Actor} attempted to set unknown role {RoleId} on user {Id}", CurrentActor, dto.RoleId, id);
            ModelState.AddModelError(nameof(dto.RoleId), "Ce rôle n'existe pas.");
            return ValidationProblem(ModelState);
        }

        user.Nom = dto.Nom;
        user.Email = dto.Email;
        user.Region = dto.Region;
        user.RoleId = dto.RoleId;

        _db.LogAudit(CurrentActor, "Update", "User", user.Id, $"email={dto.Email}, region={dto.Region}, role={role.Name}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} updated user {Id} (region={Region}, role={Role})", CurrentActor, id, dto.Region, role.Name);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        _db.Users.Remove(user);
        _db.LogAudit(CurrentActor, "Delete", "User", user.Id, $"email={user.Email}");
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Actor} deleted user {Id}", CurrentActor, id);
        return NoContent();
    }
}
