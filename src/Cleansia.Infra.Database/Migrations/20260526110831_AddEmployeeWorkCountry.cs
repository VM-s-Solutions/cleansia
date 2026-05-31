using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleansia.Infra.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeWorkCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkCountryId",
                table: "Employees",
                type: "character varying(26)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_WorkCountryId",
                table: "Employees",
                column: "WorkCountryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Countries_WorkCountryId",
                table: "Employees",
                column: "WorkCountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Countries_WorkCountryId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_WorkCountryId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "WorkCountryId",
                table: "Employees");
        }
    }
}
