# Partner Invoicing

The invoicing feature allows partners to view their earnings invoices organized by pay periods. It is implemented in the `@cleansia-partner/invoices` library with two views: the invoice list and invoice detail.

## Architecture

- `InvoicesFacade` -- Manages the invoice list, filtering, sorting, and PDF downloads
- `InvoiceDetailFacade` -- Manages individual invoice detail view
- Data is fetched via `PartnerClient.employeePayrollClient`

## Pay Periods

Invoices are organized by **pay periods** -- defined time ranges during which completed orders are aggregated into a single invoice. Each invoice belongs to a specific pay period identified by `payPeriodId` and displayed via `payPeriodLabel`.

## Invoice List

Route: `/invoices`

The invoice list displays all invoices for the current partner with:

| Column | Description |
|---|---|
| Invoice Number | Unique invoice identifier |
| Variable Symbol | Bank transfer reference |
| Pay Period | Period label (e.g., "March 2026") |
| Total Orders | Number of orders in the period |
| Sub Total | Base earnings before adjustments |
| Bonus Amount | Additional bonuses |
| Deduction Amount | Any deductions |
| Total Amount | Final payment amount |
| Currency | Payment currency code |
| Status | Current invoice status |
| Generated At | When the invoice was created |

### Invoice Statuses

| Status | Description |
|---|---|
| `Pending` | Invoice generated, awaiting approval |
| `Approved` | Approved by admin, pending payment |
| `Paid` | Payment has been made |
| `Disputed` | Partner has disputed the invoice |
| `Rejected` | Invoice was rejected |
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

- Server-side sorting via `SortDefinition[]`
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

### Additional Information

| Field | Description |
|---|---|
| `invoiceNumber` | Unique invoice identifier |
| `variableSymbol` | Variable symbol for bank transfers |
| `employeeName` | Partner's name |
| `payPeriodLabel` | Pay period description |
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
