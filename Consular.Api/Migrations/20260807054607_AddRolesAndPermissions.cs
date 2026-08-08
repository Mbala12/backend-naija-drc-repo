using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RoleId starts out defaulted to all-zeros for existing rows — overwritten by the
            // backfill below, once Roles actually exist to point at. IsAdmin is NOT dropped yet:
            // the backfill still needs to read it (together with Region) to know which of the
            // four seeded Roles each existing User should get.
            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionRole",
                columns: table => new
                {
                    PermissionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionRole", x => new { x.PermissionsId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_PermissionRole_Permissions_PermissionsId",
                        column: x => x.PermissionsId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermissionRole_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionRole_RoleId",
                table: "PermissionRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            // Fixed catalog (one row per distinct authorization gate — see
            // PermissionAuthorizationHandler) plus the four default Roles that reproduce today's
            // real (Region, IsAdmin) combinations exactly, so behavior is unchanged the moment
            // this migration applies. Same seed data as DemoDataSeeder (kept in sync by hand —
            // the seeder's own idempotent Code/Name checks mean it won't try to re-add these).
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Id"", ""Code"", ""Label"") VALUES
                    ('11111111-1111-1111-1111-111111111101', 'Demandes.View', 'Voir la liste des dossiers'),
                    ('11111111-1111-1111-1111-111111111102', 'Demandes.Transition', 'Faire progresser un dossier'),
                    ('11111111-1111-1111-1111-111111111103', 'Admin.ViewData', 'Consulter les données d''administration'),
                    ('11111111-1111-1111-1111-111111111104', 'Admin.ManageData', 'Modifier/supprimer les données d''administration'),
                    ('11111111-1111-1111-1111-111111111105', 'Applicants.Lookup', 'Rechercher un postulant par email'),
                    ('11111111-1111-1111-1111-111111111106', 'Users.Manage', 'Gérer les comptes et les rôles');

                INSERT INTO ""Roles"" (""Id"", ""Name"", ""Description"") VALUES
                    ('22222222-2222-2222-2222-222222222201', 'Agent de traitement', 'Traite les dossiers au quotidien : consultation et progression des demandes.'),
                    ('22222222-2222-2222-2222-222222222202', 'Consultation seule', 'Accès en lecture seule au tableau de bord des dossiers.'),
                    ('22222222-2222-2222-2222-222222222203', 'Administrateur système', 'Accès complet : dossiers, données d''administration et gestion des comptes.'),
                    ('22222222-2222-2222-2222-222222222204', 'Gestionnaire des comptes', 'Gère les comptes utilisateurs et les rôles, sans accès aux données d''administration.');

                INSERT INTO ""PermissionRole"" (""PermissionsId"", ""RoleId"") VALUES
                    -- Agent de traitement: Demandes.View, Demandes.Transition, Admin.ViewData, Applicants.Lookup
                    ('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201'),
                    ('11111111-1111-1111-1111-111111111102', '22222222-2222-2222-2222-222222222201'),
                    ('11111111-1111-1111-1111-111111111103', '22222222-2222-2222-2222-222222222201'),
                    ('11111111-1111-1111-1111-111111111105', '22222222-2222-2222-2222-222222222201'),
                    -- Consultation seule: Demandes.View
                    ('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222202'),
                    -- Administrateur système: all six
                    ('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222203'),
                    ('11111111-1111-1111-1111-111111111102', '22222222-2222-2222-2222-222222222203'),
                    ('11111111-1111-1111-1111-111111111103', '22222222-2222-2222-2222-222222222203'),
                    ('11111111-1111-1111-1111-111111111104', '22222222-2222-2222-2222-222222222203'),
                    ('11111111-1111-1111-1111-111111111105', '22222222-2222-2222-2222-222222222203'),
                    ('11111111-1111-1111-1111-111111111106', '22222222-2222-2222-2222-222222222203'),
                    -- Gestionnaire des comptes: Demandes.View, Users.Manage
                    ('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222204'),
                    ('11111111-1111-1111-1111-111111111106', '22222222-2222-2222-2222-222222222204');

                UPDATE ""Users"" SET ""RoleId"" = (CASE
                    WHEN ""Region"" = 0 AND ""IsAdmin"" = true  THEN '22222222-2222-2222-2222-222222222203' -- North + admin -> Administrateur système
                    WHEN ""Region"" = 0 AND ""IsAdmin"" = false THEN '22222222-2222-2222-2222-222222222201' -- North + non-admin -> Agent de traitement
                    WHEN ""Region"" = 1 AND ""IsAdmin"" = true  THEN '22222222-2222-2222-2222-222222222204' -- South + admin -> Gestionnaire des comptes
                    ELSE '22222222-2222-2222-2222-222222222202' -- South + non-admin -> Consultation seule
                END)::uuid;
            ");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill while RoleId/Roles still exist: the two seeded Roles that carried
            // Users.Manage (Administrateur système, Gestionnaire des comptes) map back to
            // IsAdmin = true, mirroring the Up() backfill in reverse.
            migrationBuilder.Sql(@"
                UPDATE ""Users"" SET ""IsAdmin"" = true
                WHERE ""RoleId"" IN ('22222222-2222-2222-2222-222222222203', '22222222-2222-2222-2222-222222222204');
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "PermissionRole");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");
        }
    }
}
