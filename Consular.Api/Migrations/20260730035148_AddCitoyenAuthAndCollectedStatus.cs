using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCitoyenAuthAndCollectedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SoumisParCitoyenId",
                table: "Demandes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotDePasseHash",
                table: "Citoyens",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Demandes_SoumisParCitoyenId",
                table: "Demandes",
                column: "SoumisParCitoyenId");

            migrationBuilder.AddForeignKey(
                name: "FK_Demandes_Citoyens_SoumisParCitoyenId",
                table: "Demandes",
                column: "SoumisParCitoyenId",
                principalTable: "Citoyens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Demandes_Citoyens_SoumisParCitoyenId",
                table: "Demandes");

            migrationBuilder.DropIndex(
                name: "IX_Demandes_SoumisParCitoyenId",
                table: "Demandes");

            migrationBuilder.DropColumn(
                name: "SoumisParCitoyenId",
                table: "Demandes");

            migrationBuilder.DropColumn(
                name: "MotDePasseHash",
                table: "Citoyens");
        }
    }
}
