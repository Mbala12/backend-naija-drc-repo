using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegionLookups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Valeur = table.Column<int>(type: "integer", nullable: false),
                    LibelleFr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LibelleEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionLookups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegionLookups_Code",
                table: "RegionLookups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegionLookups");
        }
    }
}
