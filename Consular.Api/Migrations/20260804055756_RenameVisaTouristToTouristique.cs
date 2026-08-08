using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameVisaTouristToTouristique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change. Each visa/acte sub-type is now its own TypeService (see
            // DemoDataSeeder) instead of one shared "VISA_TOURIST"/"ACTE_NAISSANCE" row covering
            // every sub-type via a free-text field on the extension table. The seeder only ever
            // INSERTs a row whose Code doesn't exist yet — it never renames one — so an existing
            // database still has the old "VISA_TOURIST" Code/Libelle unless this migration
            // updates it in place (preserving its Id, so demandes already pointing at it via
            // TypeServiceId stay valid). ACTE_NAISSANCE's Code is unchanged, only gets siblings
            // (ACTE_MARIAGE/ACTE_DECES), so it needs no rename here.
            migrationBuilder.Sql(@"
                UPDATE ""TypeServices""
                SET ""Code"" = 'VISA_TOURISTIQUE', ""Libelle"" = 'Visa touristique'
                WHERE ""Code"" = 'VISA_TOURIST';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""TypeServices""
                SET ""Code"" = 'VISA_TOURIST', ""Libelle"" = 'Visa'
                WHERE ""Code"" = 'VISA_TOURISTIQUE';
            ");
        }
    }
}
