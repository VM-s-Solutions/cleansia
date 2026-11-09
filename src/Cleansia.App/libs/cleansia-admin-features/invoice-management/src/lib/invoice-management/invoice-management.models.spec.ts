import { EmployeeInvoiceDto } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { getInvoiceTableColumns } from './invoice-management.models';

describe('invoice-management table columns', () => {
  const translate = { instant: (k: string) => k, currentLang: 'cs' } as unknown as TranslateService;
  const amountValue = (row: EmployeeInvoiceDto) =>
    getInvoiceTableColumns(translate).find((c) => c.id === 'totalAmount')?.getValue?.(row);

  it('labels the total with the currency the invoice carries, in the language of the session', () => {
    const row = EmployeeInvoiceDto.fromJS({ totalAmount: 1200, currencyCode: 'EUR' });

    expect(amountValue(row)).toBe('1 200,00 €');
  });

  // One invoice per currency and the server names it; a missing code is a bug upstream, and
  // printing crowns for it would relabel a EUR invoice on screen.
  it('prints a bare number rather than a currency the invoice does not name', () => {
    const row = EmployeeInvoiceDto.fromJS({ totalAmount: 1200 });

    expect(amountValue(row)).toBe('1 200,00');
  });

  // The server computes the transfer as the total less the cash set off: 1 250,50 - 301,00 = 949,50.
  it('prints the cash set off and the amount to transfer in the currency the invoice carries', () => {
    const row = EmployeeInvoiceDto.fromJS({
      totalAmount: 1250.5,
      cashSetOffAmount: 301,
      transferAmount: 949.5,
      currencyCode: 'CZK',
    });
    const value = (id: string) =>
      getInvoiceTableColumns(translate).find((c) => c.id === id)?.getValue?.(row);

    expect(value('totalAmount')).toBe('1\u00a0250,50\u00a0Kč');
    expect(value('cashSetOffAmount')).toBe('301,00\u00a0Kč');
    expect(value('transferAmount')).toBe('949,50\u00a0Kč');
  });

  it('offers no sort on the cash set off or the transfer, which the server cannot sort by', () => {
    const sortable = getInvoiceTableColumns(translate)
      .filter((c) => c.id === 'cashSetOffAmount' || c.id === 'transferAmount')
      .map((c) => [c.id, !!c.sortable]);

    expect(sortable).toEqual([
      ['cashSetOffAmount', false],
      ['transferAmount', false],
    ]);
  });

  it("names the pay period by its dates in the session's language, not by the server's English label", () => {
    const row = EmployeeInvoiceDto.fromJS({
      payPeriodLabel: 'Nov 01 - Nov 14, 2026',
      payPeriodStartDate: '2026-11-01',
      payPeriodEndDate: '2026-11-14',
    });
    const value = getInvoiceTableColumns(translate)
      .find((c) => c.id === 'payPeriodLabel')
      ?.getValue?.(row);

    expect(value).toBe('1. 11. 2026 – 14. 11. 2026');
    expect(value).not.toMatch(/Nov/);
  });

  // The server sorts invoices by none of these and falls back to the generation date.
  it('offers no sort on the partner, the pay period or the order count', () => {
    const sortable = getInvoiceTableColumns(translate)
      .filter((c) => ['employeeName', 'payPeriodLabel', 'totalOrders'].includes(c.id))
      .map((c) => [c.id, !!c.sortable]);

    expect(sortable).toEqual([
      ['employeeName', false],
      ['payPeriodLabel', false],
      ['totalOrders', false],
    ]);
  });
});
