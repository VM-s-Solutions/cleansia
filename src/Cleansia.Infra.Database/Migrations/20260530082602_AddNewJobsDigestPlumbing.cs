using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleansia.Infra.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNewJobsDigestPlumbing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NewJobsAvailable",
                table: "UserNotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastNewJobsDigestAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ContractStatus",
                table: "Employees",
                column: "ContractStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_ContractStatus",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NewJobsAvailable",
                table: "UserNotificationPreferences");

            migrationBuilder.DropColumn(
                name: "LastNewJobsDigestAt",
                table: "Employees");
        }
    }
}
