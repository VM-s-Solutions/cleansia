using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleansia.Infra.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTemplateEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeInvoices_InvoiceTemplates_TemplateId",
                table: "EmployeeInvoices");

            migrationBuilder.DropTable(
                name: "InvoiceTemplates");

            migrationBuilder.DropTable(
                name: "ReceiptTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeInvoices_TemplateId",
                table: "EmployeeInvoices");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "EmployeeInvoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateId",
                table: "EmployeeInvoices",
                type: "character varying(26)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlobUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceTemplates_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceTemplates_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlobUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptTemplates_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptTemplates_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_TemplateId",
                table: "EmployeeInvoices",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_CountryId_LanguageId_IsActive",
                table: "InvoiceTemplates",
                columns: new[] { "CountryId", "LanguageId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_LanguageId",
                table: "InvoiceTemplates",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_TenantId",
                table: "InvoiceTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_Country_Language_Active",
                table: "ReceiptTemplates",
                columns: new[] { "CountryId", "LanguageId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_Country_Language_Version",
                table: "ReceiptTemplates",
                columns: new[] { "CountryId", "LanguageId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_LanguageId",
                table: "ReceiptTemplates",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_TenantId",
                table: "ReceiptTemplates",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeInvoices_InvoiceTemplates_TemplateId",
                table: "EmployeeInvoices",
                column: "TemplateId",
                principalTable: "InvoiceTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
