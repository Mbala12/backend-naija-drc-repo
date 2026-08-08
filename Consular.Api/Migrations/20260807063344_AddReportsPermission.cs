using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReportsPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extends the fixed permission catalog seeded by AddRolesAndPermissions (same id
            // numbering scheme: 111...101-106 already exist) — granted only to the existing
            // "Administrateur système" role, so no other existing account silently gains report
            // access. Administrators assign it to other roles themselves via the Roles tab.
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Id"", ""Code"", ""Label"") VALUES
                    ('11111111-1111-1111-1111-111111111107', 'Reports.View', 'Consulter les rapports');

                INSERT INTO ""PermissionRole"" (""PermissionsId"", ""RoleId"") VALUES
                    ('11111111-1111-1111-1111-111111111107', '22222222-2222-2222-2222-222222222203');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""PermissionRole"" WHERE ""PermissionsId"" = '11111111-1111-1111-1111-111111111107';
                DELETE FROM ""Permissions"" WHERE ""Id"" = '11111111-1111-1111-1111-111111111107';
            ");
        }
    }
}
