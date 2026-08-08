using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixStatutFrenchLibelles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 8 of the 10 seeded Statuts were given an English Libelle by mistake (only
            // WAITING_BIOMETRICS/COLLECTED_BIOMETRICS were correctly French) — see DemoDataSeeder.
            // Libelle is the server's French source-of-truth text; the frontend's translateCode()
            // only ever substitutes an English override on top of it in EN mode, so in FR mode
            // these showed up untranslated. The seeder only INSERTs missing Codes, so an existing
            // database keeps the stale English Libelle without this.
            migrationBuilder.Sql(@"
                UPDATE ""Statuts"" SET ""Libelle"" = 'Soumis' WHERE ""Code"" = 'SUBMITTED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'En cours d''examen' WHERE ""Code"" = 'UNDER_REVIEW';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Documents manquants' WHERE ""Code"" = 'MISSING_DOCUMENTS';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Documents reçus' WHERE ""Code"" = 'DOCUMENTS_RECEIVED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Recours en cours d''examen' WHERE ""Code"" = 'APPEAL_REVIEW';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Approuvé' WHERE ""Code"" = 'APPROVED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Rejeté' WHERE ""Code"" = 'REJECTED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Collecté' WHERE ""Code"" = 'COLLECTED';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Statuts"" SET ""Libelle"" = 'Submitted' WHERE ""Code"" = 'SUBMITTED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Under Review' WHERE ""Code"" = 'UNDER_REVIEW';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Missing documents' WHERE ""Code"" = 'MISSING_DOCUMENTS';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Documents received' WHERE ""Code"" = 'DOCUMENTS_RECEIVED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Appeal review' WHERE ""Code"" = 'APPEAL_REVIEW';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Approved' WHERE ""Code"" = 'APPROVED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Rejected' WHERE ""Code"" = 'REJECTED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Collected' WHERE ""Code"" = 'COLLECTED';
            ");
        }
    }
}
