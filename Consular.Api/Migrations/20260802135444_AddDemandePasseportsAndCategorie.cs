using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandePasseportsAndCategorie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandePasseports",
                columns: table => new
                {
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeDemande = table.Column<string>(type: "text", nullable: false),
                    NumeroPasseportActuel = table.Column<string>(type: "text", nullable: true),
                    DateExpirationActuelle = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandePasseports", x => x.DemandeId);
                    table.ForeignKey(
                        name: "FK_DemandePasseports_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // The demo seeder originally seeded PASSPORT_RENEWAL under the catch-all Generique (0)
            // category, since Passeport (3) didn't exist yet. Any database seeded before this
            // migration still has that row stamped Categorie=0 — fix it up here so passport
            // demandes start getting a DemandePasseport extension row and showing up in the new
            // admin Passeport tab, without requiring a re-seed.
            migrationBuilder.Sql("UPDATE \"TypeServices\" SET \"Categorie\" = 3 WHERE \"Code\" = 'PASSPORT_RENEWAL';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"TypeServices\" SET \"Categorie\" = 0 WHERE \"Code\" = 'PASSPORT_RENEWAL';");

            migrationBuilder.DropTable(
                name: "DemandePasseports");
        }
    }
}
