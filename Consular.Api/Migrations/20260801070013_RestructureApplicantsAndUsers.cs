using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class RestructureApplicantsAndUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Demandes_Citoyens_CitoyenId",
                table: "Demandes");

            migrationBuilder.DropForeignKey(
                name: "FK_Demandes_Citoyens_SoumisParCitoyenId",
                table: "Demandes");

            // HAND-EDITED: the auto-generated migration used DropTable("Citoyens") +
            // CreateTable("Applicants"), which would have destroyed every existing
            // applicant/citoyen row. RenameTable preserves all of it — Applicant keeps every
            // Citoyen field except Region, so this is a straight rename + one column drop.
            migrationBuilder.RenameTable(
                name: "Citoyens",
                newName: "Applicants");

            migrationBuilder.RenameIndex(
                name: "IX_Citoyens_Email",
                table: "Applicants",
                newName: "IX_Applicants_Email");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Demandes");

            migrationBuilder.RenameColumn(
                name: "CitoyenId",
                table: "Demandes",
                newName: "ApplicantId");

            migrationBuilder.RenameIndex(
                name: "IX_Demandes_CitoyenId",
                table: "Demandes",
                newName: "IX_Demandes_ApplicantId");

            // HAND-EDITED: the auto-generated migration renamed this to SoumisParUserId, which
            // is wrong — every existing value here is an Applicant id (Users didn't exist before
            // this migration), so renaming it to SoumisParApplicantId is what actually preserves
            // the historical "who submitted this" data. SoumisParUserId below is a genuinely new,
            // empty column instead.
            migrationBuilder.RenameColumn(
                name: "SoumisParCitoyenId",
                table: "Demandes",
                newName: "SoumisParApplicantId");

            migrationBuilder.RenameIndex(
                name: "IX_Demandes_SoumisParCitoyenId",
                table: "Demandes",
                newName: "IX_Demandes_SoumisParApplicantId");

            migrationBuilder.AddColumn<Guid>(
                name: "SoumisParUserId",
                table: "Demandes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MotDePasseHash = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<int>(type: "integer", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Demandes_SoumisParUserId",
                table: "Demandes",
                column: "SoumisParUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Demandes_Applicants_ApplicantId",
                table: "Demandes",
                column: "ApplicantId",
                principalTable: "Applicants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Demandes_Applicants_SoumisParApplicantId",
                table: "Demandes",
                column: "SoumisParApplicantId",
                principalTable: "Applicants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Demandes_Users_SoumisParUserId",
                table: "Demandes",
                column: "SoumisParUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Demandes_Applicants_ApplicantId",
                table: "Demandes");

            migrationBuilder.DropForeignKey(
                name: "FK_Demandes_Applicants_SoumisParApplicantId",
                table: "Demandes");

            migrationBuilder.DropForeignKey(
                name: "FK_Demandes_Users_SoumisParUserId",
                table: "Demandes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Demandes_SoumisParUserId",
                table: "Demandes");

            migrationBuilder.DropColumn(
                name: "SoumisParUserId",
                table: "Demandes");

            migrationBuilder.RenameColumn(
                name: "SoumisParApplicantId",
                table: "Demandes",
                newName: "SoumisParCitoyenId");

            migrationBuilder.RenameIndex(
                name: "IX_Demandes_SoumisParApplicantId",
                table: "Demandes",
                newName: "IX_Demandes_SoumisParCitoyenId");

            migrationBuilder.RenameColumn(
                name: "ApplicantId",
                table: "Demandes",
                newName: "CitoyenId");

            migrationBuilder.RenameIndex(
                name: "IX_Demandes_ApplicantId",
                table: "Demandes",
                newName: "IX_Demandes_CitoyenId");

            migrationBuilder.AddColumn<int>(
                name: "Region",
                table: "Demandes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Region",
                table: "Applicants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.RenameIndex(
                name: "IX_Applicants_Email",
                table: "Applicants",
                newName: "IX_Citoyens_Email");

            migrationBuilder.RenameTable(
                name: "Applicants",
                newName: "Citoyens");

            migrationBuilder.AddForeignKey(
                name: "FK_Demandes_Citoyens_CitoyenId",
                table: "Demandes",
                column: "CitoyenId",
                principalTable: "Citoyens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Demandes_Citoyens_SoumisParCitoyenId",
                table: "Demandes",
                column: "SoumisParCitoyenId",
                principalTable: "Citoyens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
