using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyStatutFrenchLibelles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RECEIVED/CLOSED are inactive leftovers from an earlier statut list (not in
            // DemoDataSeeder's current active set) but still visible, English, in the admin
            // Statuts management table — same bug as FixStatutFrenchLibelles, fixed for the same
            // reason (missed there since they're Actif=false and easy to overlook).
            migrationBuilder.Sql(@"
                UPDATE ""Statuts"" SET ""Libelle"" = 'Reçu' WHERE ""Code"" = 'RECEIVED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Clôturé' WHERE ""Code"" = 'CLOSED';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Statuts"" SET ""Libelle"" = 'Received' WHERE ""Code"" = 'RECEIVED';
                UPDATE ""Statuts"" SET ""Libelle"" = 'Closed' WHERE ""Code"" = 'CLOSED';
            ");
        }
    }
}
