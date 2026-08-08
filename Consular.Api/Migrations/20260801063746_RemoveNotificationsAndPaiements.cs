using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotificationsAndPaiements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "Paiements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    DateEnvoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destinataire = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    ReferenceFournisseur = table.Column<string>(type: "text", nullable: true),
                    StatutEnvoi = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Paiements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CitoyenEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Devise = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MethodePaiement = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceTransaction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    TypeServiceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "IX_NotificationLogs_DemandeId",
                table: "NotificationLogs",
                column: "DemandeId");

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
    }
}
