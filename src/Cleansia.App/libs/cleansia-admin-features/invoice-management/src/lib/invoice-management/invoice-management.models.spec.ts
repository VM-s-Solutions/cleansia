import { EmployeeInvoiceDto } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { getInvoiceTableColumns } from './invoice-management.models';

describe('invoice-management table columns', () => {
  const translate = { instant: (k: string) => k } as unknown as TranslateService;
  const amountColumn = () =>
    getInvoiceTableColumns(translate).find((c) => c.id === 'totalAmount')!;

  it('labels the total with the code the invoice carries', () => {
    const row = EmployeeInvoiceDto.fromJS({ totalAmount: 1200, currencyCode: 'EUR' });

    expect(amountColumn().getValue!(row)).toBe('1200.00 EUR');
  });

  // One invoice per currency and the server names it; a missing code is a bug upstream, and
  // printing crowns for it would relabel a EUR invoice on screen.
  it('prints a bare number rather than a currency the invoice does not name', () => {
    const row = EmployeeInvoiceDto.fromJS({ totalAmount: 1200 });

    expect(amountColumn().getValue!(row)).toBe('1200.00');
  });
});
