using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The whole receipt, through the real renderer, in every language it is written in. The label seams
/// are asserted elsewhere; this is the check that the page they feed — header, company block, lines,
/// money block, payment block and footer — renders in each locale with every character it prints.
///
/// <para>A font without a glyph does not make QuestPDF throw: it draws a placeholder box and the render
/// succeeds, so a render that merely completed proved nothing about Czech diacritics or Cyrillic. The
/// glyph check is switched on for the render, and a character no available font can draw fails it.</para>
/// </summary>
[Collection("QuestPdfRenderer")]
public class ReceiptRenderTests
{
    private static readonly QuestPdfService Pdf = new(
        new LayoutBuilderFactory([new DefaultReceiptLayoutBuilder()], []),
        NullLogger<QuestPdfService>.Instance);

    [Theory]
    [InlineData("en")]
    [InlineData("cs")]
    [InlineData("sk")]
    [InlineData("uk")]
    [InlineData("ru")]
    public void A_Receipt_Renders_In_Every_Language_It_Is_Written_In(string languageCode)
    {
        var checkedBefore = QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        try
        {
            Assert.NotEmpty(Pdf.GenerateReceiptPdf(Receipt(languageCode)));
        }
        finally
        {
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = checkedBefore;
        }
    }

    private static ReceiptPdfData Receipt(string languageCode) =>
        new()
        {
            LanguageCode = languageCode,
            ReceiptNumber = "2026-000001",
            OrderNumber = "ORD-1",
            IssuedDate = "22.09.2026",
            CustomerName = "Jan Novák",
            CustomerEmail = "jan@example.com",
            CustomerPhone = "+420123456789",
            CustomerAddress = "Hlavní 2, Praha, 11000",
            Services = [new ReceiptLineItem("Deep clean", 1300m)],
            Packages = [new ReceiptLineItem("Move-out", 500m)],
            Extras = [new ReceiptLineItem("Windows", 200m)],
            ExpressSurcharge = 400m,
            PromoDiscount = 360m,
            Total = 2040m,
            Currency = "Kč",
            PaymentStatus = PaymentStatus.Paid,
            PaymentType = PaymentType.Cash,
            CleaningDate = "25.09.2026 10:00",
            Rooms = 2,
            Bathrooms = 1,
            EstimatedTime = 150,
            IsVatPayer = false,
            Company = new CompanyInfoData
            {
                LegalName = "Cleansia s.r.o.",
                TradingName = "Cleansia",
                RegistrationNumber = "12345678",
                VatNumber = "CZ12345678",
                Street = "Hlavní 1",
                City = "Praha",
                ZipCode = "11000",
                Address = "Hlavní 1, Praha, 11000",
                ContactInfo = "info@cleansia.cz",
                Email = "info@cleansia.cz",
                Phone = "+420 123 456 789",
            },
        };
}
