# Partner Invoicing

The invoicing feature allows partners to view their earnings invoices organized by pay periods. It is implemented in the `@cleansia-partner/invoices` library with two views: the invoice list and invoice detail.

## Architecture

- `InvoicesFacade` -- Manages the invoice list, filtering, sorting, and PDF downloads
- `InvoiceDetailFacade` -- Manages individual invoice detail view
- Data is fetched via `PartnerClient.employeePayrollClient`

## Pay Periods

Invoices are organized by **pay periods** -- defined time ranges during which completed orders are aggregated into a single invoice. Each invoice belongs to a specific pay period identified by `payPeriodId` and displayed from `payPeriodStartDate` and `payPeriodEndDate`, calendar days printed in the partner's language (`1. 11. 2026 – 14. 11. 2026`). A period lasts at most 14 days and closes by itself the night after its end date; its invoices appear within about 20 minutes of the close, and each is e-mailed to the cleaner. The old `payPeriodLabel` still travels on the wire for one release and nothing reads it. A cancelled invoice is replaced by a corrected one with a new number within minutes, and the company pays an invoice by bank transfer, so no screen shows a payout date. → [Pay and payouts](/flows/pay-and-payouts)

## Invoice List

Route: `/invoices`

The invoice list displays all invoices for the current partner with:

| Column | Description |
|---|---|
| Invoice Number | Unique invoice identifier |
| Pay Period | The period's first and last day, in the partner's language |
| Generated Date | When the invoice was created |
| Total Orders | Number of orders in the invoice |
| Total Amount | The invoice total, in the invoice's currency |
| Cash Set Off | The cash the cleaner held that was set off against this invoice (`cashSetOffAmount`) |
| Bank Transfer to You | What the company transfers: the total less the cash set off (`transferAmount`) |
| Status | Current invoice status |

The web list shows no total. The Android and iOS invoice lists carry a **Total earned** figure →
[Mobile app — partner dashboard](/mobile-app/features#partner-dashboard).

### Invoice Statuses

| Status | Description |
|---|---|
| `Pending` | Invoice generated, awaiting approval |
| `Approved` | Approved by admin, pending payment |
| `Paid` | Payment has been made |
| `Disputed` | Partner has disputed the invoice |
| `Rejected` | Written before 2026-11; no action produces it any more, and the value stays in the enum |
| `Cancelled` | Invoice was cancelled |

### Filtering

Partners can filter invoices by:

| Filter | Type |
|---|---|
| `invoiceNumber` | Text search |
| `minAmount` / `maxAmount` | Amount range |
| `dateFrom` / `dateTo` | Date range |
| `payPeriodId` | Specific pay period |
| `statuses` | One or more `EmployeeInvoiceStatus` values |

### Sorting & Pagination

- Server-side sorting via `SortDefinition[]`: Invoice Number, Generated Date, Total Amount and Status sort. Pay Period, Total Orders, Cash Set Off and Bank Transfer to You offer no sort, because the server sorts by none of them and would fall back to the generation date
- A failed list read keeps the rows on screen, and the shared interceptor toasts why; after a failed partner lookup the next load (a filter, a sort) asks for the partner again
- Server-side pagination with configurable offset and limit
- Sorting/filter changes reset to the first page

## Invoice Detail

Route: `/invoices/:id`

The invoice detail page shows complete invoice information:

### Earnings Breakdown

| Field | Description |
|---|---|
| `subTotal` | Base earnings from completed orders |
| `bonusAmount` | Performance bonuses or adjustments |
| `deductionAmount` | Deductions (penalties, advance repayments, etc.) |
| `totalAmount` | `subTotal + bonusAmount - deductionAmount` |
| Cash set off | Shown as a minus under the total when `cashSetOffAmount > 0` |
| Bank transfer to you | Shown in bold, ruled off, under it; with no cash set off the statement ends at the total |

### Additional Information

| Field | Description |
|---|---|
| `invoiceNumber` | Unique invoice identifier |
| `variableSymbol` | Variable symbol for bank transfers |
| `employeeName` | Partner's name |
| Pay period | The period's two days in the partner's language, a dash when the server sent none |
| `status` | Current status |
| `generatedAt` | Generation timestamp |
| `approvedAt` | Approval timestamp (if approved) |
| `approvedBy` | Admin who approved (if approved) |
| `paidAt` | Payment timestamp (if paid) |
| `adminNotes` | Notes from admin |
| `bankTransferNote` | Bank transfer reference note |

## PDF Download

Partners can download invoice PDFs from both the list and detail views:

```typescript
downloadPdf(): void {
  this.partnerClient.employeePayrollClient
    .downloadInvoice(invoiceId)
    .subscribe(fileResponse => {
      const blob = fileResponse.data;
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileResponse.fileName || `invoice-${invoiceNumber}.pdf`;
      document.body.appendChild(link);
      link.click();
      // cleanup...
    });
}
```

::: info
PDF download is only available when the `pdfBlobName` field is set on the invoice. If the PDF has not been generated yet, the download button is disabled and an appropriate message is shown.
:::

## Print Support

The invoice detail page supports printing via `window.print()`, allowing partners to print the invoice directly from their browser.

## Error Handling

- If the invoice is not found (404), a "not found" message is displayed
- If loading fails, a generic error message is shown
- Both facades use `signal`-based error state for reactive error display
