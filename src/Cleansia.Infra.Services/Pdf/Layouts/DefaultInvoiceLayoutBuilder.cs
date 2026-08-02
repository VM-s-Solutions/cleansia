using System.Globalization;
using Cleansia.Infra.Services.Pdf.Components;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

/// <summary>
/// The payout invoice: the cleaner supplies the work and is paid for it, so the cleaner is the
/// SUPPLIER and Cleansia is the CUSTOMER. Every block below is oriented that way — the masthead, the
/// two party blocks and, most consequentially, the bank details in the payment block.
/// </summary>
public class DefaultInvoiceLayoutBuilder : IInvoiceLayoutBuilder
{
    public virtual string CountryCode => "default";

    protected virtual InvoiceLabels Labels { get; } = new();

    protected virtual CultureInfo NumberCulture => CultureInfo.InvariantCulture;

    public virtual void Build(IDocumentContainer container, InvoicePdfData data, CountryInvoiceContext? context)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(CleansiaPdfTheme.FontSizeBody));

            // The letterhead is part of the flow, not a running header: repeating a 25%-tall masthead
            // on every continuation page is what pushed a three-line invoice onto two pages.
            page.Content().Column(col =>
            {
                col.Item().Element(c => BuildHeader(c, data));
                col.Item().PaddingHorizontal(30).PaddingVertical(6).Element(c => BuildContent(c, data, context));
            });

            page.Footer().PaddingHorizontal(30).PaddingBottom(12).Element(f => BuildFooter(f, data));
        });
    }

    protected virtual void BuildHeader(IContainer container, InvoicePdfData data)
    {
        container.GradientHeader(
            data.Supplier.Name,
            null,
            meta =>
            {
                meta.Column(col =>
                {
                    col.Item().Element(c => c.MetaField(Labels.InvoiceNumber, data.InvoiceNumber));
                    col.Item().Element(c => c.MetaField(Labels.IssueDate, FormatDate(data.GeneratedAt)));

                    if (data.DueDate.HasValue)
                        col.Item().Element(c => c.MetaField(Labels.DueDate, FormatDate(data.DueDate.Value)));
                });
            },
            padding: 18,
            nameFontSize: 20);
    }

    protected virtual void BuildContent(IContainer container, InvoicePdfData data, CountryInvoiceContext? context)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(8).Text(Labels.DocumentTitle)
                .FontSize(16).Bold().FontColor(CleansiaPdfTheme.TextPrimary);

            col.Item().Element(c => BuildPartySection(c, data));

            col.Item().Element(c => c.SectionTitle(Labels.PaymentDetails));
            col.Item().Element(c => BuildPaymentSection(c, data));

            col.Item().Element(c => c.SectionTitle(Labels.LineItems));
            col.Item().Element(c => BuildLineItemsTable(c, data));

            // A total split across a page break is the one block that must never break.
            col.Item().PaddingTop(CleansiaPdfTheme.SmallPadding).ShowEntire().Row(row =>
            {
                row.RelativeItem().PaddingRight(20).Element(c =>
                {
                    if (!string.IsNullOrWhiteSpace(data.LegalDisclaimer))
                        c.AlignBottom().LegalNoticeBox(data.LegalDisclaimer, Labels.LegalNotice);
                });

                row.ConstantItem(280).Element(c => BuildSummary(c, data));
            });
        });
    }

    protected virtual void BuildPartySection(IContainer container, InvoicePdfData data)
    {
        container.TwoColumnInfoSection(
            left =>
            {
                left.Column(col =>
                {
                    col.Item().Element(c => c.BlockTitle(Labels.Supplier));
                    foreach (var field in SupplierFields(data))
                        col.Item().Element(c => c.InlineField(field.Label, field.Value));

                    col.Item().PaddingTop(8).Element(c => c.BlockTitle(Labels.ContactDetails, CleansiaPdfTheme.FontSizeMetaValue));
                    foreach (var field in ContactFields(data))
                        col.Item().Element(c => c.InlineField(field.Label, field.Value));
                });
            },
            right =>
            {
                right.Column(col =>
                {
                    col.Item().Element(c => c.BlockTitle(Labels.Customer));
                    foreach (var field in CustomerFields(data))
                        col.Item().Element(c => c.InlineField(field.Label, field.Value));

                    col.Item().PaddingTop(8).Element(c => c.BlockTitle(Labels.PayPeriod, CleansiaPdfTheme.FontSizeMetaValue));
                    col.Item().Element(c => c.InlineField(Labels.PayPeriodFrom, data.PayPeriodStart));
                    col.Item().Element(c => c.InlineField(Labels.PayPeriodTo, data.PayPeriodEnd));
                });
            });
    }

    protected virtual IReadOnlyList<InvoiceField> SupplierFields(InvoicePdfData data)
    {
        var supplier = data.Supplier;
        var fields = new List<InvoiceField>
        {
            new(Labels.Name, supplier.Name),
            new(Labels.Address, FormatAddress(supplier)),
            new(Labels.Country, supplier.Country),
            new(Labels.RegistrationNumber, supplier.RegistrationNumber)
        };

        if (supplier.IsVatPayer)
        {
            fields.Add(new InvoiceField(Labels.VatNumber, supplier.VatNumber));
        }
        else
        {
            fields.Add(new InvoiceField(Labels.VatStatus, Labels.NotVatRegistered));
        }

        return fields;
    }

    protected virtual IReadOnlyList<InvoiceField> ContactFields(InvoicePdfData data) =>
    [
        new(Labels.Email, data.Supplier.Email),
        new(Labels.Phone, data.Supplier.Phone)
    ];

    protected virtual IReadOnlyList<InvoiceField> CustomerFields(InvoicePdfData data)
    {
        var company = data.Company;
        if (company == null) return [];

        return
        [
            new InvoiceField(Labels.Name, company.LegalName),
            new InvoiceField(Labels.Address, company.Address),
            new InvoiceField(Labels.RegistrationNumber, company.RegistrationNumber),
            new InvoiceField(Labels.VatNumber, company.VatNumber)
        ];
    }

    protected virtual IReadOnlyList<InvoiceField> PaymentFields(InvoicePdfData data)
    {
        var supplier = data.Supplier;
        var fields = new List<InvoiceField>();

        if (!string.IsNullOrWhiteSpace(supplier.BankName))
            fields.Add(new InvoiceField(Labels.BankName, supplier.BankName));

        fields.Add(new InvoiceField(Labels.BankAccount, supplier.BankAccountNumber));
        fields.Add(new InvoiceField(Labels.Iban, supplier.Iban));
        fields.Add(new InvoiceField(Labels.Swift, supplier.Swift));

        if (!string.IsNullOrWhiteSpace(data.VariableSymbol))
            fields.Add(new InvoiceField(Labels.VariableSymbol, data.VariableSymbol));

        if (!string.IsNullOrWhiteSpace(data.ConstantSymbol))
            fields.Add(new InvoiceField(Labels.ConstantSymbol, data.ConstantSymbol));

        fields.Add(new InvoiceField(Labels.PaymentMethod, Labels.BankTransfer));
        fields.Add(new InvoiceField(Labels.AmountDue, FormatMoney(data.TotalAmount, data)));

        return fields;
    }

    protected virtual void BuildPaymentSection(IContainer container, InvoicePdfData data)
    {
        container.FieldGrid(PaymentFields(data), columns: 3);
    }

    protected virtual void BuildLineItemsTable(IContainer container, InvoicePdfData data)
    {
        container.OrderDetailsTable(
            [Labels.Description, Labels.Quantity, Labels.Unit, Labels.UnitPrice, Labels.LineTotal],
            [6, 2, 2, 3, 3],
            table =>
            {
                for (var i = 0; i < data.LineItems.Count; i++)
                {
                    var line = data.LineItems[i];
                    table.TableCell(DescribeLine(line), i);
                    table.TableCell(line.Quantity.ToString("0.##", NumberCulture), i, alignRight: true);
                    table.TableCell(Labels.UnitOfMeasure, i);
                    table.TableCell(FormatMoney(line.UnitPrice, data), i, alignRight: true);
                    table.TableCell(FormatMoney(line.LineTotal, data), i, alignRight: true);
                }
            });
    }

    protected virtual string DescribeLine(InvoiceLineItem line) =>
        $"{Labels.LineDescription} {line.OrderNumber} ({FormatDate(line.PerformedOn)})";

    protected virtual void BuildSummary(IContainer container, InvoicePdfData data)
    {
        var lines = new List<(string Label, string Value, bool IsBold)>
        {
            (Labels.SubTotal, FormatMoney(data.SubTotal, data), false)
        };

        if (data.BonusAmount != 0)
            lines.Add((Labels.Bonus, $"+{FormatMoney(data.BonusAmount, data)}", false));

        if (data.DeductionAmount != 0)
            lines.Add((Labels.Deduction, $"-{FormatMoney(data.DeductionAmount, data)}", false));

        if (data.VatAmount != 0)
            lines.Add((Labels.Vat, FormatMoney(data.VatAmount, data), false));

        lines.Add((Labels.AmountDue, FormatMoney(data.TotalAmount, data), true));

        container.Column(col =>
        {
            col.Item().SummaryBox(lines);

            if (!data.Supplier.IsVatPayer)
            {
                col.Item().PaddingTop(6).AlignRight()
                    .Text(Labels.NotVatRegistered)
                    .FontSize(CleansiaPdfTheme.FontSizeLabel)
                    .FontColor(CleansiaPdfTheme.TextSecondary)
                    .Italic();
            }
        });
    }

    protected virtual void BuildFooter(IContainer container, InvoicePdfData data)
    {
        container.BorderTop(1).BorderColor(CleansiaPdfTheme.BorderLight)
            .PaddingTop(CleansiaPdfTheme.InnerPadding)
            .AlignCenter()
            .Column(col =>
            {
                if (data.Company != null)
                {
                    col.Item().AlignCenter().Text($"{data.Company.LegalName} · {data.Company.ContactInfo}")
                        .FontSize(CleansiaPdfTheme.FontSizeSmall)
                        .FontColor(CleansiaPdfTheme.TextSecondary);
                }

                col.Item().PaddingTop(4).AlignCenter()
                    .Text($"{Labels.GeneratedOn}: {data.GeneratedAt:dd.MM.yyyy HH:mm} UTC")
                    .FontSize(CleansiaPdfTheme.FontSizeSmall)
                    .FontColor(CleansiaPdfTheme.TextSecondary);
            });
    }

    protected virtual string FormatMoney(decimal amount, InvoicePdfData data) =>
        $"{data.CurrencySymbol}{amount.ToString("N2", NumberCulture)}";

    protected virtual string FormatDate(DateTime value) => value.ToString("dd.MM.yyyy", NumberCulture);

    private static string? FormatAddress(InvoiceSupplierData supplier)
    {
        var cityLine = string.Join(" ", new[] { supplier.ZipCode, supplier.City }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        var lines = new[] { supplier.Street, cityLine }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return lines.Length > 0 ? string.Join('\n', lines) : null;
    }
}
