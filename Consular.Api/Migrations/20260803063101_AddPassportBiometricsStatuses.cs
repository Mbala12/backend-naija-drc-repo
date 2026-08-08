using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPassportBiometricsStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change — Statuts is still just Code/Libelle/Ordre/EstFinal/Actif rows.
            // The demo seeder (DemoDataSeeder.Seed) inserts WAITING_BIOMETRICS/COLLECTED_BIOMETRICS
            // on next boot since those Codes don't exist yet, but it only ever INSERTS missing rows
            // — it never updates an existing row's Ordre. Any database seeded before this migration
            // still has MISSING_DOCUMENTS..COLLECTED stamped with their old Ordre (4..9), which would
            // leave the two new passport-only steps sharing an Ordre with UNDER_REVIEW/MISSING_DOCUMENTS
            // instead of sitting between them. Shift the existing rows up by 2 to make room.
            migrationBuilder.Sql(@"
                UPDATE ""Statuts""
                SET ""Ordre"" = ""Ordre"" + 2
                WHERE ""Code"" IN ('MISSING_DOCUMENTS', 'DOCUMENTS_RECEIVED', 'APPEAL_REVIEW', 'APPROVED', 'REJECTED', 'COLLECTED');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Statuts""
                SET ""Ordre"" = ""Ordre"" - 2
                WHERE ""Code"" IN ('MISSING_DOCUMENTS', 'DOCUMENTS_RECEIVED', 'APPEAL_REVIEW', 'APPROVED', 'REJECTED', 'COLLECTED');
            ");
        }
    }
}
