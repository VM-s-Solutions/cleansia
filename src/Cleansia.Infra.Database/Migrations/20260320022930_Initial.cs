using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleansia.Infra.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Translations = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Code = table.Column<string>(type: "citext", maxLength: 5, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeValue = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Code = table.Column<string>(type: "citext", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                    table.UniqueConstraint("AK_Languages_Code", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Translations = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayPeriods",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PerRoomPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedTime = table.Column<int>(type: "integer", nullable: false),
                    Translations = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Street = table.Column<string>(type: "citext", maxLength: 255, nullable: false),
                    City = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyInfo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradingName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tagline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VatNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Street = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Iban = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Swift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyInfo_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CountryConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DefaultCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DefaultLanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhonePrefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StandardVatRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ReducedVatRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    TaxIdLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxIdFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultPaymentGateway = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LegalRequirementsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryConfigurations_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CountryInvoiceConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    VatRequired = table.Column<bool>(type: "boolean", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    DigitalSignatureRequired = table.Column<bool>(type: "boolean", nullable: false),
                    EInvoiceFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AdditionalFieldsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LegalDisclaimerTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryInvoiceConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryInvoiceConfigs_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplateTranslations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    EmailType = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplateTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplateTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailTranslations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Header = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SubHeader = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    GreetingWord = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Instruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CodeNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Footer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EmailType = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FirstName = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    GoogleId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ResetPasswordCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    ResetPasswordCodeExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Profile = table.Column<int>(type: "integer", nullable: false),
                    AuthenticationType = table.Column<int>(type: "integer", nullable: false),
                    ProfilePhotoName = table.Column<string>(type: "text", nullable: true),
                    ConfirmationCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    ConfirmationCodeExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PreferredLanguageCode = table.Column<string>(type: "citext", maxLength: 5, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Languages_PreferredLanguageCode",
                        column: x => x.PreferredLanguageCode,
                        principalTable: "Languages",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    PackageId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    BasePay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraPerRoom = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExtraPerBathroom = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    DistanceRatePerKm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CurrencyId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    MinimumPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    MaximumPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePayConfigs_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayConfigs_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayConfigs_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageServices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PackageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageServices_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", nullable: false),
                    Platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeviceToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IBAN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AverageRating = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ComplaintsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ContractStatus = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PassportId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NationalityId = table.Column<string>(type: "character varying(26)", nullable: true),
                    EmergencyContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AddressId = table.Column<string>(type: "character varying(26)", nullable: true),
                    UserId = table.Column<string>(type: "character varying(26)", nullable: false),
                    PreferredCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Availability = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Countries_NationalityId",
                        column: x => x.NationalityId,
                        principalTable: "Countries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GdprRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RequestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProcessedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GdprRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GdprRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerAddressId = table.Column<string>(type: "character varying(26)", nullable: false),
                    DisplayOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rooms = table.Column<int>(type: "integer", nullable: false),
                    Bathrooms = table.Column<int>(type: "integer", nullable: false),
                    CleaningDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentType = table.Column<int>(type: "integer", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedTime = table.Column<int>(type: "integer", nullable: false),
                    ActualCompletionTime = table.Column<int>(type: "integer", nullable: true),
                    CompletionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EmployeePayCalculated = table.Column<bool>(type: "boolean", nullable: false),
                    TravelDistance = table.Column<decimal>(type: "numeric", nullable: true),
                    RequiredEmployees = table.Column<int>(type: "integer", nullable: false),
                    MaxEmployees = table.Column<int>(type: "integer", nullable: false),
                    ConfirmationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StripeSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SpecialInstructions = table.Column<string>(type: "text", nullable: true),
                    AccessInstructions = table.Column<string>(type: "text", nullable: true),
                    CurrencyId = table.Column<string>(type: "character varying(26)", nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", nullable: true),
                    ReceiptId = table.Column<string>(type: "text", nullable: true),
                    Extras = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Addresses_CustomerAddressId",
                        column: x => x.CustomerAddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserConsents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ConsentType = table.Column<int>(type: "integer", nullable: false),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserConsents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartPackageItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CartId = table.Column<string>(type: "character varying(26)", nullable: false),
                    PackageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartPackageItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartPackageItems_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartPackageItems_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartServiceItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CartId = table.Column<string>(type: "character varying(26)", nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartServiceItems_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartServiceItems_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    PreviousVersionId = table.Column<string>(type: "character varying(26)", nullable: true),
                    EmployeeId = table.Column<string>(type: "character varying(26)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_EmployeeDocuments_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "EmployeeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeInvoices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    PayPeriodId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalOrders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SubTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    DeductionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    PdfBlobUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfGenerationFailed = table.Column<bool>(type: "boolean", nullable: false),
                    PdfGenerationError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PdfGenerationAttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TemplateId = table.Column<string>(type: "character varying(26)", nullable: true),
                    CountryId = table.Column<string>(type: "character varying(26)", nullable: true),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VariableSymbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SpecificSymbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankTransferNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeInvoices_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInvoices_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInvoices_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInvoices_InvoiceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InvoiceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInvoices_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInvoices_PayPeriods_PayPeriodId",
                        column: x => x.PayPeriodId,
                        principalTable: "PayPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Disputes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolvedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StripeDisputeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disputes_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderEmployees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderEmployees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderEmployees_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ReportedByEmployeeId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderIssues_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderNotes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderNotes_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPackages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", nullable: false),
                    PackageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPackages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderPackages_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPhotos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    PhotoType = table.Column<int>(type: "integer", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedByEmployeeId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPhotos_Employees_CapturedByEmployeeId",
                        column: x => x.CapturedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderPhotos_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderReceipts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LanguageId = table.Column<string>(type: "character varying(26)", nullable: false),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    EmailSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailMessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderReceipts_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReceipts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderReviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderReviews_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderServices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "text", nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderEmployeePays",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    PayPeriodId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    BasePay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtrasPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExpensesPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    BonusPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    DeductionPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TotalPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PayBreakdown = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    EmployeeInvoiceId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeactivatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEmployeePays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderEmployeePays_EmployeeInvoices_EmployeeInvoiceId",
                        column: x => x.EmployeeInvoiceId,
                        principalTable: "EmployeeInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderEmployeePays_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderEmployeePays_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderEmployeePays_PayPeriods_PayPeriodId",
                        column: x => x.PayPeriodId,
                        principalTable: "PayPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisputeEvidence",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DisputeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UploadedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeEvidence_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisputeMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DisputeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsStaffMessage = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeMessages_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CountryId",
                table: "Addresses",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_TenantId",
                table: "Addresses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CartPackageItems_CartId",
                table: "CartPackageItems",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_CartPackageItems_PackageId",
                table: "CartPackageItems",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_TenantId",
                table: "Carts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartServiceItems_CartId",
                table: "CartServiceItems",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_CartServiceItems_ServiceId",
                table: "CartServiceItems",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInfo_CountryId_IsActive",
                table: "CompanyInfo",
                columns: new[] { "CountryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInfo_IsActive",
                table: "CompanyInfo",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInfo_RegistrationNumber",
                table: "CompanyInfo",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInfo_TenantId",
                table: "CompanyInfo",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_TenantId",
                table: "Countries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CountryConfigurations_CountryId",
                table: "CountryConfigurations",
                column: "CountryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountryConfigurations_TenantId",
                table: "CountryConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CountryInvoiceConfigs_CountryId",
                table: "CountryInvoiceConfigs",
                column: "CountryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_TenantId",
                table: "Currencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceId",
                table: "Devices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId",
                table: "Devices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_DeviceId",
                table: "Devices",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_DisputeId",
                table: "DisputeEvidence",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_UploadedOn",
                table: "DisputeEvidence",
                column: "UploadedOn");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_CreatedOn",
                table: "DisputeMessages",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_DisputeId",
                table: "DisputeMessages",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_CreatedOn",
                table: "Disputes",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OrderId",
                table: "Disputes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Status",
                table: "Disputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_TenantId",
                table: "Disputes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_UserId",
                table: "Disputes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateTranslations_LanguageId",
                table: "EmailTemplateTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateTranslations_TenantId",
                table: "EmailTemplateTranslations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateTranslations_Type_Language_Key",
                table: "EmailTemplateTranslations",
                columns: new[] { "EmailType", "LanguageId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTranslations_LanguageId",
                table: "EmailTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTranslations_TenantId",
                table: "EmailTranslations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_DocumentType",
                table: "EmployeeDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                table: "EmployeeDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_DocumentType",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_PreviousVersionId",
                table: "EmployeeDocuments",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_Status",
                table: "EmployeeDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_TenantId",
                table: "EmployeeDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_CountryId",
                table: "EmployeeInvoices",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_CurrencyId",
                table: "EmployeeInvoices",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_EmployeeId",
                table: "EmployeeInvoices",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_EmployeeId_PayPeriodId",
                table: "EmployeeInvoices",
                columns: new[] { "EmployeeId", "PayPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_InvoiceNumber",
                table: "EmployeeInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_LanguageId",
                table: "EmployeeInvoices",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_PayPeriodId",
                table: "EmployeeInvoices",
                column: "PayPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_Status",
                table: "EmployeeInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_Status_GeneratedAt",
                table: "EmployeeInvoices",
                columns: new[] { "Status", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_TemplateId",
                table: "EmployeeInvoices",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_TenantId",
                table: "EmployeeInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvoices_VariableSymbol",
                table: "EmployeeInvoices",
                column: "VariableSymbol",
                unique: true,
                filter: "\"VariableSymbol\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayConfigs_CurrencyId",
                table: "EmployeePayConfigs",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayConfigs_PackageId",
                table: "EmployeePayConfigs",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayConfigs_ServiceId",
                table: "EmployeePayConfigs",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayConfigs_ServiceId_PackageId",
                table: "EmployeePayConfigs",
                columns: new[] { "ServiceId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayConfigs_TenantId",
                table: "EmployeePayConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_AddressId",
                table: "Employees",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_NationalityId",
                table: "Employees",
                column: "NationalityId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                table: "Employees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Name_Scope_ScopeValue",
                table: "FeatureFlags",
                columns: new[] { "Name", "Scope", "ScopeValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_TenantId",
                table: "FeatureFlags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GdprRequests_TenantId",
                table: "GdprRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GdprRequests_UserId",
                table: "GdprRequests",
                column: "UserId");

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
                name: "IX_OrderEmployeePays_EmployeeId",
                table: "OrderEmployeePays",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployeePays_EmployeeId_PayPeriodId",
                table: "OrderEmployeePays",
                columns: new[] { "EmployeeId", "PayPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployeePays_EmployeeInvoiceId",
                table: "OrderEmployeePays",
                column: "EmployeeInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployeePays_OrderId",
                table: "OrderEmployeePays",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployeePays_OrderId_EmployeeId",
                table: "OrderEmployeePays",
                columns: new[] { "OrderId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployeePays_PayPeriodId",
                table: "OrderEmployeePays",
                column: "PayPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployeePays_TenantId",
                table: "OrderEmployeePays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployees_EmployeeId",
                table: "OrderEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmployees_OrderId",
                table: "OrderEmployees",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderIssues_OrderId",
                table: "OrderIssues",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderIssues_TenantId",
                table: "OrderIssues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderNotes_OrderId",
                table: "OrderNotes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderNotes_TenantId",
                table: "OrderNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPackages_OrderId",
                table: "OrderPackages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPackages_PackageId",
                table: "OrderPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPhotos_CapturedByEmployeeId",
                table: "OrderPhotos",
                column: "CapturedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPhotos_Order_PhotoType",
                table: "OrderPhotos",
                columns: new[] { "OrderId", "PhotoType" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPhotos_OrderId",
                table: "OrderPhotos",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPhotos_TenantId",
                table: "OrderPhotos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReceipts_LanguageId",
                table: "OrderReceipts",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReceipts_Order_Language",
                table: "OrderReceipts",
                columns: new[] { "OrderId", "LanguageId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReceipts_OrderId",
                table: "OrderReceipts",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderReceipts_ReceiptNumber",
                table: "OrderReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderReceipts_TenantId",
                table: "OrderReceipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReviews_OrderId",
                table: "OrderReviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReviews_OrderId_UserId",
                table: "OrderReviews",
                columns: new[] { "OrderId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderReviews_TenantId",
                table: "OrderReviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CurrencyId",
                table: "Orders",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerAddressId",
                table: "Orders",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId",
                table: "Orders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServices_OrderId",
                table: "OrderServices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServices_ServiceId",
                table: "OrderServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId",
                table: "OrderStatusHistory",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_TenantId",
                table: "Packages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageServices_PackageId",
                table: "PackageServices",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageServices_ServiceId",
                table: "PackageServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriods_EndDate",
                table: "PayPeriods",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriods_StartDate",
                table: "PayPeriods",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriods_StartDate_EndDate",
                table: "PayPeriods",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriods_Status",
                table: "PayPeriods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriods_TenantId",
                table: "PayPeriods",
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

            migrationBuilder.CreateIndex(
                name: "IX_Services_TenantId",
                table: "Services",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_TenantId",
                table: "TenantConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_TenantId_Key",
                table: "TenantConfigurations",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_TenantId",
                table: "UserConsents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_UserId_ConsentType",
                table: "UserConsents",
                columns: new[] { "UserId", "ConsentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreferredLanguageCode",
                table: "Users",
                column: "PreferredLanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartPackageItems");

            migrationBuilder.DropTable(
                name: "CartServiceItems");

            migrationBuilder.DropTable(
                name: "CompanyInfo");

            migrationBuilder.DropTable(
                name: "CountryConfigurations");

            migrationBuilder.DropTable(
                name: "CountryInvoiceConfigs");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DisputeEvidence");

            migrationBuilder.DropTable(
                name: "DisputeMessages");

            migrationBuilder.DropTable(
                name: "EmailTemplateTranslations");

            migrationBuilder.DropTable(
                name: "EmailTranslations");

            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "EmployeePayConfigs");

            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "GdprRequests");

            migrationBuilder.DropTable(
                name: "OrderEmployeePays");

            migrationBuilder.DropTable(
                name: "OrderEmployees");

            migrationBuilder.DropTable(
                name: "OrderIssues");

            migrationBuilder.DropTable(
                name: "OrderNotes");

            migrationBuilder.DropTable(
                name: "OrderPackages");

            migrationBuilder.DropTable(
                name: "OrderPhotos");

            migrationBuilder.DropTable(
                name: "OrderReceipts");

            migrationBuilder.DropTable(
                name: "OrderReviews");

            migrationBuilder.DropTable(
                name: "OrderServices");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "PackageServices");

            migrationBuilder.DropTable(
                name: "ReceiptTemplates");

            migrationBuilder.DropTable(
                name: "TenantConfigurations");

            migrationBuilder.DropTable(
                name: "UserConsents");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "Disputes");

            migrationBuilder.DropTable(
                name: "EmployeeInvoices");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "InvoiceTemplates");

            migrationBuilder.DropTable(
                name: "PayPeriods");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
