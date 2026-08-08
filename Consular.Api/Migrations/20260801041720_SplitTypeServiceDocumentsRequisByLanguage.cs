using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitTypeServiceDocumentsRequisByLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DocumentsRequis",
                table: "TypeServices",
                newName: "DocumentsRequisFr");

            migrationBuilder.AddColumn<List<string>>(
                name: "DocumentsRequisEn",
                table: "TypeServices",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentsRequisEn",
                table: "TypeServices");

            migrationBuilder.RenameColumn(
                name: "DocumentsRequisFr",
                table: "TypeServices",
                newName: "DocumentsRequis");
        }
    }
}
