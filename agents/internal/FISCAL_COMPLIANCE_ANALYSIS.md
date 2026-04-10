# Fiscal Compliance Analysis — Czech Republic & EU

> **Date:** 2026-04-10
> **Scope:** Order creation flow, payments (Stripe + cash), receipt generation
> **Question:** Does Cleansia need EET / fiscal integration today, and how should it be implemented?

---

## TL;DR

**Your current implementation is correct for CZ in 2026.** No urgent code changes needed.

- ❌ EET was abolished on **January 1, 2023** by Act 458/2022
- ❌ No real-time fiscal reporting obligation exists in CZ today
- ✅ Standard VAT invoicing rules apply (which your code already handles)
- ⚠️ **EET 2.0 launches January 1, 2027** — start preparing the architecture this year
- ⚠️ Verify Azure Blob has **10-year retention** on the receipts container (Czech tax law requirement)

---

## Czech Regulatory Timeline

| Period | Status | Impact on Cleansia |
|---|---|---|
| 2016–2020 | EET 1.0 active | Required real-time reporting + FIK on receipts |
| March 2020 | Suspended (COVID-19) | No reporting required |
| **Jan 1, 2023** | **Abolished by Act 458/2022** | EET system completely shut down |
| 2023–2026 (now) | No fiscal obligation | Standard VAT invoicing only |
| **Jan 1, 2027** | **EET 2.0 launches** (planned) | Will need integration |

### EET 2.0 — What's Coming in 2027

EET 2.0 details are still being finalized but it will be:

- **Digital-first** — no required receipt printing
- **Optional for small businesses** below certain thresholds (TBD)
- **Real-time API** similar to old EET (POST receipt → receive FIK code)
- **Tax breaks** for entrepreneurs who voluntarily participate early

**Sources:**
- [Act 458/2022 (zakonyprolidi.cz)](https://www.zakonyprolidi.cz/cs/2016-112)
- [EET 2.0 Comprehensive Guide](https://www.fiscal-requirements.com/news/5087)
- [Czech Radio: Finance Minister Announces EET Return in 2027](https://english.radio.cz/finance-minister-announces-reintroduction-electronic-sales-reporting-2027-8878122)

---

## Current Cleansia Implementation (Verified)

```
Customer creates order
        │
        ├─── Card payment ──→ Stripe Checkout Session ──→ webhook ──→ Order.Paid
        │                                                        │
        └─── Cash payment ──→ Order.Confirmed ────────────────────┤
                                                                  │
                                                                  ▼
                                          Background job (GenerateReceiptFunction)
                                                                  │
                                                                  ▼
                                          ReceiptService creates OrderReceipt
                                             ├─ Sequential number: RCP-2026-0001
                                             ├─ PDF generated via QuestPDF
                                             ├─ Stored in Azure Blob
                                             └─ Emailed to customer
```

### Key Files

| File | Purpose |
|---|---|
| `Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs` (lines 244-282) | Order creation, branches by payment type |
| `Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs` (lines 132-150) | Stripe webhook, queues receipt generation |
| `Cleansia.Functions/Functions/GenerateReceiptFunction.cs` | Azure Function background job |
| `Cleansia.Core.AppServices/Services/ReceiptService.cs` (lines 22-137) | PDF generation + blob upload |
| `Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs` | QuestPDF receipt layout |
| `Cleansia.Core.Domain/Receipts/OrderReceipt.cs` | DB entity for receipts |
| `Cleansia.Core.Domain/Company/CompanyInfo.cs` | Stores LegalName, RegistrationNumber (IČO), VatNumber (DIČ) |

### Receipt Number Format

Defined in `Cleansia.Core.AppServices/Common/Constants.cs`:

```csharp
public class ReceiptNumberFormat
{
    public const string Pattern = "RCP-{0}-{1:D4}"; // RCP-YYYY-NNNN
}
```

Example: `RCP-2026-0001`, `RCP-2026-0002`, ... Sequential per calendar year, generated via `OrderReceiptRepository.GetNextSequenceForYearAsync()`.

---

## Czech VAT Compliance Check

These are the requirements under Czech VAT law for B2C receipts in 2026:

| Required by law | Current implementation |
|---|---|
| Issue within 15 days of transaction | ✅ Generated immediately on payment webhook |
| Business legal name | ✅ `CompanyInfo.LegalName` |
| Business VAT ID (DIČ) | ✅ `CompanyInfo.VatNumber` |
| Business registration number (IČO) | ✅ `CompanyInfo.RegistrationNumber` |
| Sequential invoice number | ✅ `RCP-{Year}-{NNNN}` pattern |
| Date of issuance | ✅ `IssuedAt` field |
| Customer name + address | ✅ Captured during checkout |
| Description of services | ✅ Service/Package list with prices |
| Total amount + currency | ✅ Order total |
| Stored for 10 years | ⚠️ **Verify Azure Blob retention policy** |

### Standard VAT Rates (CZ, 2026)

- **21%** standard rate (cleaning services fall here)
- **12%** reduced rate (catering, books, some medical, public transport)
- **0%** exports

---

## Gaps in Current Implementation

### 1. VAT line items missing in PDF

Your current receipt PDF shows `CompanyInfo.VatNumber` but does NOT calculate or display VAT breakdown lines. If Cleansia is VAT-registered (annual turnover above 2M CZK), the receipt MUST show:

- Net amount (price excluding VAT)
- VAT rate (21% for cleaning services)
- VAT amount
- Gross total (net + VAT)

**If Cleansia is NOT yet VAT-registered**, the receipt must include the text **"Nejsme plátci DPH"** ("Not a VAT payer") so the customer cannot deduct VAT from their accounting.

### 2. Blob retention policy unclear

Czech law requires **10-year retention** for receipts and tax documents. Verify Azure Blob has the appropriate retention policy on the container holding receipts.

Check via Azure CLI:
```bash
az storage container show \
  --name receipts \
  --account-name <storage-account> \
  --query "properties.legalHold,properties.immutabilityPolicy"
```

If no policy exists, set one:
```bash
az storage container immutability-policy create \
  --container-name receipts \
  --account-name <storage-account> \
  --period 3650
```

### 3. No fiscal abstraction layer

There is currently no `IFiscalService` interface. When EET 2.0 launches in January 2027, the receipt generation flow will need to call a fiscal authority API and store the returned code on the receipt. Without an abstraction, this means modifying `ReceiptService` directly.

---

## What Cleansia Does NOT Need Today

- ❌ **EET integration** — abolished, no system to integrate with
- ❌ **FIK codes on receipts** — Financial Administration doesn't issue them anymore
- ❌ **Real-time reporting endpoint** — no API exists to call
- ❌ **Fiscal middleware (Fiskaly etc.)** — only needed for SK/DE/AT, not CZ today
- ❌ **Stripe Connect for fiscal** — Stripe doesn't handle CZ fiscal anyway

---

## Recommended Action Plan

### Priority 1 — This Week (Quick Wins)

#### 1.1 Add VAT calculation to receipts (if VAT-registered)

If Cleansia is VAT-registered, add a `VatRate` field to `CompanyInfo`:

```csharp
public decimal? VatRate { get; private set; }  // null = not VAT-registered, e.g., 21.00m for 21%
```

Update `DefaultReceiptLayoutBuilder` to:
- Calculate VAT lines when `VatRate` is set
- Show "Nejsme plátci DPH" when `VatRate` is null

Update receipt PDF to display:
```
Subtotal (excl. VAT)    1 000 Kč
VAT 21%                   210 Kč
─────────────────────────────────
Total (incl. VAT)       1 210 Kč
```

#### 1.2 Verify blob retention

Set 10-year immutability policy on the Azure Blob container holding receipts.

### Priority 2 — Q3-Q4 2026 (EET 2.0 Preparation)

#### 2.1 Add `IFiscalService` abstraction

Create a thin interface that allows zero-code-change swap when EET 2.0 launches.

**New file: `Cleansia.Core.AppServices/Services/IFiscalService.cs`**

```csharp
public interface IFiscalService
{
    Task<FiscalResult> RegisterReceiptAsync(OrderReceipt receipt, CancellationToken ct);
}

public record FiscalResult(
    bool HasFiscalCode,
    string? Code,
    string? RegisteredAt,
    string? ErrorMessage)
{
    public static FiscalResult NotRequired() => new(false, null, null, null);
    public static FiscalResult Success(string code, string registeredAt) =>
        new(true, code, registeredAt, null);
    public static FiscalResult Error(string error) =>
        new(false, null, null, error);
}
```

**New file: `Cleansia.Infra.Services/Fiscal/NoOpFiscalService.cs`** (today's implementation)

```csharp
public class NoOpFiscalService : IFiscalService
{
    public Task<FiscalResult> RegisterReceiptAsync(
        OrderReceipt receipt, CancellationToken ct)
        => Task.FromResult(FiscalResult.NotRequired());
}
```

**Wire it into `ReceiptService.GenerateReceiptAsync`** between PDF generation and blob upload:

```csharp
var fiscalResult = await _fiscalService.RegisterReceiptAsync(receipt, ct);
if (fiscalResult.HasFiscalCode)
    receipt.SetFiscalCode(fiscalResult.Code!);
```

**Add `FiscalCode` field to `OrderReceipt` entity:**

```csharp
public string? FiscalCode { get; private set; }
public DateTime? FiscalRegisteredAt { get; private set; }

public void SetFiscalCode(string code)
{
    FiscalCode = code;
    FiscalRegisteredAt = DateTime.UtcNow;
}
```

**Update PDF builder to conditionally show fiscal section:**

```csharp
if (!string.IsNullOrEmpty(data.FiscalCode))
{
    column.Item().Text($"FIK: {data.FiscalCode}").FontSize(8);
}
```

When EET 2.0 launches, create `CzechEet2FiscalService : IFiscalService`, swap the DI registration, and you're compliant. Zero changes to other code.

#### 2.2 Add `FiscalCode` field to receipt entity (database migration)

`MANUAL_STEP`: EF migration to add `FiscalCode` and `FiscalRegisteredAt` columns to `OrderReceipts` table. Owner runs migration.

### Priority 3 — Only If Expanding Outside CZ

If Cleansia expands to other countries, fiscal middleware becomes mandatory:

| Country | System | Status | Provider Recommendation |
|---|---|---|---|
| 🇨🇿 Czech Republic | None today, EET 2.0 in 2027 | Not yet required | Build own (then EET 2.0 lib) |
| 🇸🇰 Slovakia | **eKasa** | **Mandatory** | [Fiskaly](https://fiskaly.com) or Slovak provider |
| 🇩🇪 Germany | **TSE (KassenSichV)** | **Mandatory** | [Fiskaly](https://fiskaly.com) supports this natively |
| 🇦🇹 Austria | **RKSV** | **Mandatory** | [Fiskaly](https://fiskaly.com) supports this natively |
| 🇭🇺 Hungary | NAV Online Invoice | **Mandatory for B2B** | NAV API direct or 3rd party |
| 🇪🇸 Spain | VeriFactu (nationwide) | Effective July 2025 | Spanish providers |

The `IFiscalService` abstraction lets you do:

```csharp
var service = _fiscalServiceFactory.GetFor(order.Country.IsoCode);
var fiscalResult = await service.RegisterReceiptAsync(receipt, ct);
```

Each country implementation is one new class, no other changes needed.

**Cost note:** Fiskaly charges roughly €0.01–€0.05 per transaction. Cheaper than building country-specific integrations from scratch for <5,000 transactions/month.

---

## Summary

| Item | Status | Action |
|---|---|---|
| EET integration today | ✅ Not required | None |
| Czech VAT receipt fields | ✅ Mostly compliant | Verify VAT calculation if VAT-registered |
| 10-year receipt retention | ⚠️ Unclear | Set Azure Blob immutability policy |
| EET 2.0 (2027) preparation | ❌ Not started | Add `IFiscalService` abstraction this year |
| EU expansion fiscal | ❌ Not implemented | Use Fiskaly when expanding to SK/DE/AT |

**Bottom line:** Your code is fine. Don't add fiscal middleware now — it's an unnecessary cost. But add the `IFiscalService` interface this year so EET 2.0 in January 2027 is a one-line swap, not a refactor.
