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
});
