using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaiementsAndServiceFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MontantFrais",
                table: "TypeServices",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Paiements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceTransaction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TypeServiceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Devise = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    MethodePaiement = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CitoyenEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paiements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paiements_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paiements_DemandeId",
                table: "Paiements",
                column: "DemandeId");

            migrationBuilder.CreateIndex(
                name: "IX_Paiements_ReferenceTransaction",
                table: "Paiements",
                column: "ReferenceTransaction",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Paiements");

            migrationBuilder.DropColumn(
                name: "MontantFrais",
                table: "TypeServices");
        }
    }
}
