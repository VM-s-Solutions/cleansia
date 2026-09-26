using Cleansia.Infra.Services.Pdf.Components;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public class DefaultReceiptLayoutBuilder : IReceiptLayoutBuilder
{
    public virtual string CountryCode => "default";

    /// <summary>
    /// The words, chosen from the document's own language. Every label on the page comes from here;
    /// what the layout prints of its own is the sale's data.
    /// </summary>
    protected virtual ReceiptLabels LabelsFor(ReceiptPdfData data) => ReceiptLabels.For(data.LanguageCode);

    public virtual void Build(IDocumentContainer container, ReceiptPdfData data)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(CleansiaPdfTheme.FontSizeBody));

            page.Header().Element(h => BuildHeader(h, data));
            page.Content().PaddingHorizontal(30).PaddingVertical(10).Element(c => BuildContent(c, data));
            page.Footer().PaddingHorizontal(30).PaddingBottom(20).Element(f => BuildFooter(f, data));
        });
    }

    protected virtual void BuildHeader(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        container.GradientHeader(
            data.Company?.TradingName ?? "CLEANSIA",
            data.Company?.Tagline,
            meta =>
            {
                meta.Column(col =>
                {
                    col.Item().Element(c => c.MetaField(labels.ReceiptNumber, data.ReceiptNumber));
                    col.Item().Element(c => c.MetaField(labels.OrderNumber, data.OrderNumber));
                    col.Item().Element(c => c.MetaField(labels.IssuedDate, data.IssuedDate));
                });
            });
    }

    protected virtual void BuildContent(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        container.Column(col =>
        {
            col.Item().Element(c => c.DocumentTitle(labels.DocumentTitle));

            col.Item().Element(c => BuildInfoSection(c, data));

            if (data.CleaningDate != null || data.Rooms != null || data.Bathrooms != null)
            {
                col.Item().Element(c => c.SectionTitle(labels.OrderDetails));
                col.Item().Element(c => BuildOrderDetails(c, data));
            }

            col.Item().Element(c => c.SectionTitle(labels.Items));
            col.Item().Element(c => BuildItemsTable(c, data));

            col.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing).Element(c => BuildSummary(c, data));

            col.Item().Element(c => BuildPaymentInfo(c, data));

            // Fiscal registration block is only rendered when the receipt was registered with a fiscal authority.
            if (!string.IsNullOrWhiteSpace(data.FiscalCode))
            {
                col.Item().Element(c => BuildFiscalInfo(c, data));
            }
        });
    }

    protected virtual void BuildFiscalInfo(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        container.PaddingTop(CleansiaPdfTheme.SectionSpacing)
            .Column(col =>
            {
                col.Item().Element(c => c.SectionTitle(labels.FiscalRegistration));

                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(labels.FiscalCode)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextSecondary);
                        c.Item().Text(data.FiscalCode ?? string.Empty)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary)
                            .Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(data.FiscalProviderKey))
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(labels.FiscalProvider)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextSecondary);
                            c.Item().Text(data.FiscalProviderKey)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(data.FiscalRegisteredAt))
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(labels.FiscalRegisteredAt)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextSecondary);
                            c.Item().Text(data.FiscalRegisteredAt)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                        });
                    }
                });
            });
    }

    /// <summary>
    /// The issuer's block, as label/value pairs, so what it states can be asserted without rendering.
    ///
    /// <para><b>The VAT number is printed only on a sale that charged VAT.</b> A receipt carrying both
    /// the statutory non-payer notice and a VAT number asserts two contradictory things about the same
    /// seller. <see cref="ReceiptPdfData.IsVatPayer"/> is the order's own frozen posture, and a VAT
    /// number left on the company row of a non-payer is stale configuration this document must not
    /// publish.</para>
    /// </summary>
    protected virtual IReadOnlyList<(string Label, string? Value)> CompanyFields(ReceiptPdfData data)
    {
        if (data.Company is not { } company)
        {
            return [];
        }

        var labels = LabelsFor(data);
        var fields = new List<(string Label, string? Value)> { (labels.Company, company.LegalName) };

        if (!string.IsNullOrWhiteSpace(company.RegistrationNumber))
            fields.Add((labels.RegistrationNumber, company.RegistrationNumber));

        if (data.IsVatPayer && !string.IsNullOrWhiteSpace(company.VatNumber))
            fields.Add((labels.VatNumber, company.VatNumber));

        fields.Add((labels.Address, company.Address));
        fields.Add((labels.Contact, company.ContactInfo));

        if (!string.IsNullOrWhiteSpace(company.Iban))
            fields.Add((labels.Iban, company.Iban));

        return fields;
    }

    /// <summary>
    /// The statutory non-payer notice under the money block, on a sale that charged no VAT. At most one
    /// of it and the VAT number in <see cref="CompanyFields"/> is printed: a VAT sale whose issuer row
    /// holds no number prints neither.
    /// </summary>
    protected virtual string? VatNotice(ReceiptPdfData data) =>
        data.IsVatPayer ? null : LabelsFor(data).NotVatRegistered;

    protected virtual void BuildInfoSection(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        container.TwoColumnInfoSection(
            left =>
            {
                left.Column(col =>
                {
                    col.Item().Text(labels.CustomerInformation)
                        .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                        .Bold()
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                    col.Item().PaddingTop(6);
                    col.Item().Element(c => c.LabeledField(labels.Name, data.CustomerName));
                    col.Item().Element(c => c.LabeledField(labels.Email, data.CustomerEmail));
                    col.Item().Element(c => c.LabeledField(labels.Phone, data.CustomerPhone));
                    col.Item().Element(c => c.LabeledField(labels.Address, data.CustomerAddress));
                });
            },
            right =>
            {
                right.Column(col =>
                {
                    col.Item().Text(labels.CompanyInformation)
                        .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                        .Bold()
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                    col.Item().PaddingTop(6);

                    foreach (var (label, value) in CompanyFields(data))
                        col.Item().Element(c => c.LabeledField(label, value));
                });
            });
    }

    protected virtual void BuildOrderDetails(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        container.PaddingTop(10)
            .Background(CleansiaPdfTheme.LightBlue)
            .Padding(14)
            .Row(row =>
            {
                if (data.CleaningDate != null)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.CleaningDate)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.CleaningDate)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }

                if (data.Rooms.HasValue)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.Rooms)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.Rooms.Value.ToString())
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }

                if (data.Bathrooms.HasValue)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.Bathrooms)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.Bathrooms.Value.ToString())
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }

                if (data.EstimatedTime.HasValue)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.EstimatedDuration)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(Duration(data.EstimatedTime.Value, labels))
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }
            });
    }

    protected static string Duration(int minutes, ReceiptLabels labels)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return hours > 0
            ? mins > 0 ? $"{hours} {labels.HoursUnit} {mins} {labels.MinutesUnit}" : $"{hours} {labels.HoursUnit}"
            : $"{mins} {labels.MinutesUnit}";
    }

    /// <summary>
    /// Every priced line of the sale, in the order the price was actually built: the catalogue lines at
    /// what they cost, then the express surcharge that was applied to their sum, then each discount
    /// that came off it.
    ///
    /// <para><b>These sum to <see cref="ReceiptPdfData.Total"/>, and that is the point</b> — to the cent,
    /// because the order stores each term at the precision its column keeps.</para>
    ///
    /// <para>Composed as label/amount pairs rather than inside the QuestPDF container for the same
    /// reason as <see cref="SummaryLines"/>: these are the numbers a customer will add up by hand, and
    /// a figure composed inside a container is assertable only by rendering a PDF and reading it
    /// back.</para>
    /// </summary>
    protected virtual IReadOnlyList<(string Description, decimal Amount)> ItemLines(ReceiptPdfData data)
    {
        var labels = LabelsFor(data);
        var lines = new List<(string Description, decimal Amount)>();

        foreach (var service in data.Services)
        {
            lines.Add((service.Name, service.Price));
        }

        foreach (var package in data.Packages)
        {
            lines.Add(($"{package.Name} ({labels.PackageSuffix})", package.Price));
        }

        foreach (var extra in data.Extras)
        {
            lines.Add((extra.Name, extra.Price));
        }

        if (data.ExpressSurcharge != 0m)
        {
            lines.Add((labels.ExpressSurcharge, data.ExpressSurcharge));
        }

        if (data.TierDiscount != 0m)
        {
            lines.Add((labels.TierDiscount, -data.TierDiscount));
        }

        if (data.MembershipDiscount != 0m)
        {
            lines.Add((labels.MembershipDiscount, -data.MembershipDiscount));
        }

        if (data.PromoDiscount != 0m)
        {
            lines.Add((labels.PromoDiscount, -data.PromoDiscount));
        }

        return lines;
    }

    protected virtual void BuildItemsTable(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);
        var lines = ItemLines(data);

        container.StyledTable(
            [labels.Description, labels.Amount],
            table =>
            {
                if (lines.Count == 0)
                {
                    table.TableCell(labels.NoItems, 0);
                    table.TableCell("—", 0, alignRight: true);
                    return;
                }

                var rowIndex = 0;
                foreach (var (description, amount) in lines)
                {
                    table.TableCell(description, rowIndex);
                    table.TableCell(Money(labels, data.Currency, amount), rowIndex, alignRight: true);
                    rowIndex++;
                }
            });
    }

    /// <summary>
    /// An amount as the document's language writes it ("2 000,00 Kč", "Kč2,000.00"), in the label set's
    /// culture rather than the host's: a Czech reader takes the comma in "2,000.00" for a decimal point.
    /// The sign leads the whole figure, never sitting between the unit and its digits.
    /// </summary>
    private static string Money(ReceiptLabels labels, string currency, decimal amount)
    {
        var digits = Math.Abs(amount).ToString("N2", labels.NumberCulture);
        var unsigned = string.IsNullOrEmpty(currency)
            ? digits
            : labels.CurrencyAfterAmount ? $"{digits} {currency}" : $"{currency}{digits}";

        return amount < 0m ? $"-{unsigned}" : unsigned;
    }

    /// <summary>
    /// The money block, as label/value/bold triples.
    ///
    /// <para>Separate from <see cref="BuildSummary"/> for the same reason the invoice builder's
    /// <c>SummaryLines</c> is: these are the numbers a customer will hold against their bank
    /// statement, and composing them inside a QuestPDF container makes them assertable only by
    /// rendering a PDF and reading it back.</para>
    /// </summary>
    protected virtual IReadOnlyList<(string Label, string Value, bool IsBold)> SummaryLines(
        ReceiptPdfData data)
    {
        var labels = LabelsFor(data);
        var lines = new List<(string Label, string Value, bool IsBold)>();

        // If the company is a VAT payer and we have a breakdown, show net + VAT + gross.
        // Otherwise, show only the gross total (and the non-VAT-payer notice below).
        if (data.IsVatPayer && data.VatAmount.HasValue && data.VatAmount.Value > 0)
        {
            var netAmount = data.NetAmount ?? data.Total - data.VatAmount.Value;
            // VatRate is a FRACTION (0.21), so it is scaled here rather than formatted directly.
            // `:N0` on 0.21 rounds to zero and printed "VAT 0%" next to a non-zero VAT amount — a
            // defective tax document, and the only place the convention was visible to a customer.
            var vatRateDisplay = data.VatRate.HasValue
                ? $" {(data.VatRate.Value * 100m).ToString("N0", labels.NumberCulture)}%"
                : string.Empty;

            lines.Add((labels.SubtotalExcludingVat, Money(labels, data.Currency, netAmount), false));
            lines.Add(($"{labels.Vat}{vatRateDisplay}", Money(labels, data.Currency, data.VatAmount.Value), false));
        }

        lines.Add((labels.Total, Money(labels, data.Currency, data.Total), true));

        // How the total was SETTLED, under the total itself. Only when credit was used: on every other
        // receipt these rows would state the obvious, and the block is deliberately short.
        //
        // TOTAL DOES NOT MOVE. Credit is a tender, not a discount — the sale keeps its size and the VAT
        // block above it is the taxable base the fiscal authority registered. These two answer the
        // different question the customer is actually asking, which is why their card statement is
        // lighter than the number at the top.
        //
        // The card line is last and bold because it is the figure they will go looking for.
        if (data.CreditApplied > 0)
        {
            lines.Add((labels.PaidWithCredit, Money(labels, data.Currency, -data.CreditApplied), false));
            lines.Add((labels.PaidByCard, Money(labels, data.Currency, data.AmountDueOnCard), true));
        }

        return lines;
    }

    protected virtual void BuildSummary(IContainer container, ReceiptPdfData data)
    {
        var lines = SummaryLines(data);
        var notice = VatNotice(data);

        container.Column(col =>
        {
            col.Item().SummaryBox(lines);

            if (notice is not null)
            {
                col.Item().PaddingTop(6)
                    .Text(notice)
                    .FontSize(CleansiaPdfTheme.FontSizeLabel)
                    .FontColor(CleansiaPdfTheme.TextSecondary)
                    .Italic();
            }
        });
    }

    /// <summary>
    /// How the sale stands, as label/value pairs, each value the document language's word for the
    /// payment status and the tender actually taken — never the enum's own name.
    /// </summary>
    protected virtual IReadOnlyList<(string Label, string Value)> PaymentLines(ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        return
        [
            (labels.PaymentStatus, labels.PaymentStatuses[data.PaymentStatus]),
            (labels.PaymentMethod, labels.PaymentTypes[data.PaymentType]),
        ];
    }

    protected virtual void BuildPaymentInfo(IContainer container, ReceiptPdfData data)
    {
        var lines = PaymentLines(data);

        container.PaddingTop(CleansiaPdfTheme.SectionSpacing)
            .Background(CleansiaPdfTheme.TableHeaderBg)
            .Padding(14)
            .Row(row =>
            {
                foreach (var (label, value) in lines)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(label)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(value)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary)
                            .Bold();
                    });
                }
            });
    }

    protected virtual void BuildFooter(IContainer container, ReceiptPdfData data)
    {
        var labels = LabelsFor(data);

        var contactInfo = data.Company != null
            ? $"{labels.Contact}: {data.Company.Email} | {data.Company.Phone}"
            : null;

        container.StandardFooter(
            data.Company?.TradingName ?? "CLEANSIA",
            contactInfo,
            thanksBeforeName: labels.ThanksBeforeName,
            thanksAfterName: labels.ThanksAfterName,
            generatedLabel: labels.GeneratedOn,
            generatedAt: DateTime.UtcNow);
    }
}
