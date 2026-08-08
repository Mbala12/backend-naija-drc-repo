using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Citoyens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Region = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Citoyens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Statuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    EstFinal = table.Column<bool>(type: "boolean", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statuts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypeServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Categorie = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Actif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Demandes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroReference = table.Column<string>(type: "text", nullable: false),
                    TypeServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitoyenId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatutId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanalDepot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Attributs = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    Region = table.Column<int>(type: "integer", nullable: false),
                    EquipeAssignee = table.Column<string>(type: "text", nullable: false),
                    NoteDocumentsManquantes = table.Column<string>(type: "text", nullable: true),
                    DateDepot = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Demandes_Citoyens_CitoyenId",
                        column: x => x.CitoyenId,
                        principalTable: "Citoyens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Demandes_Statuts_StatutId",
                        column: x => x.StatutId,
                        principalTable: "Statuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Demandes_TypeServices_TypeServiceId",
                        column: x => x.TypeServiceId,
                        principalTable: "TypeServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DemandeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    CheminStockage = table.Column<string>(type: "text", nullable: false),
                    DocumentKind = table.Column<string>(type: "text", nullable: false),
                    ValideParAgent = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandeDocuments_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandeEtatCivils",
                columns: table => new
                {
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeActe = table.Column<string>(type: "text", nullable: false),
                    DateEvenement = table.Column<DateOnly>(type: "date", nullable: false),
                    LieuEvenement = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandeEtatCivils", x => x.DemandeId);
                    table.ForeignKey(
                        name: "FK_DemandeEtatCivils_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandeHistoriques",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatutOrigineId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatutDestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    DateChangement = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandeHistoriques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandeHistoriques_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemandeHistoriques_Statuts_StatutDestinationId",
                        column: x => x.StatutDestinationId,
                        principalTable: "Statuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandeHistoriques_Statuts_StatutOrigineId",
                        column: x => x.StatutOrigineId,
                        principalTable: "Statuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DemandeVisas",
                columns: table => new
                {
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeVisa = table.Column<string>(type: "text", nullable: false),
                    PaysDestination = table.Column<string>(type: "text", nullable: false),
                    DateEntreePrevue = table.Column<DateOnly>(type: "date", nullable: false),
                    DureeSejourJours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandeVisas", x => x.DemandeId);
                    table.ForeignKey(
                        name: "FK_DemandeVisas_Demandes_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "Demandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    Destinataire = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    StatutEnvoi = table.Column<int>(type: "integer", nullable: false),
                    ReferenceFournisseur = table.Column<string>(type: "text", nullable: true),
                    DateEnvoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Citoyens_Email",
                table: "Citoyens",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandeDocuments_DemandeId",
                table: "DemandeDocuments",
                column: "DemandeId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandeHistoriques_DemandeId_DateChangement",
                table: "DemandeHistoriques",
                columns: new[] { "DemandeId", "DateChangement" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandeHistoriques_StatutDestinationId",
                table: "DemandeHistoriques",
                column: "StatutDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandeHistoriques_StatutOrigineId",
                table: "DemandeHistoriques",
                column: "StatutOrigineId");

            migrationBuilder.CreateIndex(
                name: "IX_Demandes_CitoyenId",
                table: "Demandes",
                column: "CitoyenId");

            migrationBuilder.CreateIndex(
                name: "IX_Demandes_NumeroReference",
                table: "Demandes",
                column: "NumeroReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Demandes_StatutId",
                table: "Demandes",
                column: "StatutId");

            migrationBuilder.CreateIndex(
                name: "IX_Demandes_TypeServiceId",
                table: "Demandes",
                column: "TypeServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DemandeId",
                table: "NotificationLogs",
                column: "DemandeId");

            migrationBuilder.CreateIndex(
                name: "IX_Statuts_Code",
                table: "Statuts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TypeServices_Code",
                table: "TypeServices",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandeDocuments");

            migrationBuilder.DropTable(
                name: "DemandeEtatCivils");

            migrationBuilder.DropTable(
                name: "DemandeHistoriques");

            migrationBuilder.DropTable(
                name: "DemandeVisas");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "Demandes");

            migrationBuilder.DropTable(
                name: "Citoyens");

            migrationBuilder.DropTable(
                name: "Statuts");

            migrationBuilder.DropTable(
                name: "TypeServices");
        }
    }
}
