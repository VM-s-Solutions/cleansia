namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// ADR-0002 D3.4 — a single (PayPeriod, Employee) pairing that has committed
/// pay (an <c>OrderEmployeePay</c> row) but NO <c>EmployeeInvoice</c> for <c>(PayPeriodId, EmployeeId)</c>
/// and is older than the threshold. The reconciliation sweep re-enqueues a <c>generate-invoice</c>
/// message keyed <c>invoice:{PayPeriodId}:{EmployeeId}</c> (ADR-0002 D2.1) for each item.
///
/// <para><see cref="TenantId"/> rides along (read cross-tenant via <c>IgnoreQueryFilters</c>) so the
/// sweep can set the tenant override per item and stamp the re-enqueued envelope's TenantId correctly.</para>
/// </summary>
public sealed record InvoiceReconciliationItem(string PayPeriodId, string EmployeeId, string? TenantId);
