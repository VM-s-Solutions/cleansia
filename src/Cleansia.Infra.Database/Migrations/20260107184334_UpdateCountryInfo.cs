using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleansia.Infra.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCountryInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CountryId",
                table: "CompanyInfo",
                type: "character varying(26)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInfo_CountryId_IsActive",
                table: "CompanyInfo",
                columns: new[] { "CountryId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInfo_Countries_CountryId",
                table: "CompanyInfo",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInfo_Countries_CountryId",
                table: "CompanyInfo");

            migrationBuilder.DropIndex(
                name: "IX_CompanyInfo_CountryId_IsActive",
                table: "CompanyInfo");

            migrationBuilder.AlterColumn<string>(
                name: "CountryId",
                table: "CompanyInfo",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(26)");
        }
    }
}
