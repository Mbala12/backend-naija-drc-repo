using Consular.Api.Services;
using Xunit;

namespace Consular.Api.Tests;

public class DemoDataSeederTests
{
    [Fact]
    public void BuildSeedData_ReturnsDistinctApplicantsUsersAndDemands()
    {
        var seeder = new DemoDataSeeder();

        var data = seeder.BuildSeedData();

        Assert.NotEmpty(data.Regions);
        Assert.NotEmpty(data.TypeServices);
        Assert.NotEmpty(data.Statuts);
        Assert.Equal(4, data.Applicants.Count);
        Assert.Equal(2, data.Users.Count);
        Assert.Equal(4, data.Demandes.Count);
        Assert.Equal(data.Applicants.Count, data.Applicants.Select(a => a.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(data.Users.Count, data.Users.Select(u => u.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // No account should ever share an email across the two tables — that would make login
        // ambiguous, since AuthController.Login checks Applicants first, then Users, by email.
        Assert.Empty(data.Applicants.Select(a => a.Email).Intersect(data.Users.Select(u => u.Email), StringComparer.OrdinalIgnoreCase));
        Assert.Equal(data.Demandes.Count, data.Demandes.Select(d => d.NumeroReference).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void BuildSeedData_LoginCapableAccountsHaveAWorkingPasswordHash()
    {
        var seeder = new DemoDataSeeder();

        var data = seeder.BuildSeedData();

        // The two Users (staff/admin) always need real credentials — there is no beneficiary-
        // only concept for that table.
        Assert.All(data.Users, u => Assert.False(string.IsNullOrWhiteSpace(u.MotDePasseHash)));

        // Roles aren't wired onto data.Users here (BuildSeedData builds them before any Role is
        // persisted — see Seed()), but the Role shape itself is verifiable: distinct names/codes,
        // and exactly one seeded Role ("Administrateur système") holds every permission — the
        // bootstrap role Admin Nord ends up with, the only way to create further Users/Roles on
        // a fresh system.
        Assert.Equal(data.Permissions.Count, data.Permissions.Select(p => p.Code).Distinct().Count());
        Assert.Equal(data.Roles.Count, data.Roles.Select(r => r.Name).Distinct().Count());
        Assert.Single(data.Roles, r => r.Permissions.Count == data.Permissions.Count);

        // Among Applicants, some are login-capable (real hash) and some are beneficiary-only
        // (null hash, per the existing design) — both cases should exist in the seed data.
        Assert.Contains(data.Applicants, a => a.MotDePasseHash is not null);
        Assert.Contains(data.Applicants, a => a.MotDePasseHash is null);
    }
}
