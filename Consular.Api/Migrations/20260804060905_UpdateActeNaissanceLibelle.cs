using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateActeNaissanceLibelle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same gap as RenameVisaTouristToTouristique: ACTE_NAISSANCE's Code didn't change when
            // its Marriage/Death siblings were introduced (see DemoDataSeeder), but its Libelle did
            // ("Acte" -> "Acte de naissance") so it reads correctly next to "Acte de mariage"/"Acte
            // de décès" instead of the old generic single-acte-service label. The seeder only
            // INSERTs missing Codes, so an existing database keeps the stale Libelle without this.
            migrationBuilder.Sql(@"
                UPDATE ""TypeServices""
                SET ""Libelle"" = 'Acte de naissance'
                WHERE ""Code"" = 'ACTE_NAISSANCE' AND ""Libelle"" = 'Acte';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""TypeServices""
                SET ""Libelle"" = 'Acte'
                WHERE ""Code"" = 'ACTE_NAISSANCE' AND ""Libelle"" = 'Acte de naissance';
            ");
        }
    }
}
