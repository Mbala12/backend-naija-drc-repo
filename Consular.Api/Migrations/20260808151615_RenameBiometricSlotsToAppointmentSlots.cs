using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consular.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameBiometricSlotsToAppointmentSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiometricSlotTemplates");

            migrationBuilder.CreateTable(
                name: "AppointmentSlotTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Region = table.Column<int>(type: "integer", nullable: false),
                    Categorie = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CapaciteMax = table.Column<int>(type: "integer", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSlotTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlotTemplates_Region_Categorie_DayOfWeek_StartTi~",
                table: "AppointmentSlotTemplates",
                columns: new[] { "Region", "Categorie", "DayOfWeek", "StartTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentSlotTemplates");

            migrationBuilder.CreateTable(
                name: "BiometricSlotTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CapaciteMax = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    Region = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricSlotTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricSlotTemplates_Region_DayOfWeek_StartTime",
                table: "BiometricSlotTemplates",
                columns: new[] { "Region", "DayOfWeek", "StartTime" },
                unique: true);
        }
    }
}
