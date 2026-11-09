import { EmployeeInvoiceDto, EmployeeInvoiceStatus, IEmployeeInvoiceDto } from '@cleansia/partner-services';
import { getInvoicesTableDefinition } from './invoices.models';

function invoice(overrides: Partial<IEmployeeInvoiceDto>): EmployeeInvoiceDto {
  return EmployeeInvoiceDto.fromJS({
    id: 'inv-1',
    employeeId: 'emp-1',
    employeeName: 'Jana',
    payPeriodId: 'pp-1',
    payPeriodLabel: 'Nov 01 - Nov 14, 2026',
    payPeriodStartDate: '2026-11-01',
    payPeriodEndDate: '2026-11-14',
    invoiceNumber: 'INV-1',
    variableSymbol: '1',
    totalOrders: 1,
    subTotal: 1200,
    bonusAmount: 0,
    deductionAmount: 0,
    totalAmount: 1200,
    cashSetOffAmount: 0,
    transferAmount: 1200,
    currencyCode: 'EUR',
    status: EmployeeInvoiceStatus.Pending,
    generatedAt: '2026-09-01T00:00:00Z',
    ...overrides,
  });
}

describe('getInvoicesTableDefinition — total amount', () => {
  const totalOf = (row: EmployeeInvoiceDto): unknown => {
    const column = getInvoicesTableDefinition({ onDownload: jest.fn() }, 'cs').columns.find(
      (c) => c.id === 'totalAmount'
    );
    if (!column?.getValue) throw new Error('totalAmount column missing or static');
    return column.getValue(row);
  };

  it('labels the total with the currency the invoice carries, in the language of the session', () => {
    expect(totalOf(invoice({ currencyCode: 'EUR' }))).toBe('1\u00a0200,00\u00a0€');
  });

  // One invoice per currency and the server names it; a missing code is a bug upstream, and
  // printing crowns for it would relabel a EUR invoice on screen.
  it('prints a bare number rather than a currency the invoice does not name', () => {
    expect(totalOf(invoice({ currencyCode: '' }))).toBe('1\u00a0200,00');
  });
});

describe('getInvoicesTableDefinition — period, sorts and the transfer', () => {
  const columnOf = (id: string) => {
    const column = getInvoicesTableDefinition({ onDownload: jest.fn() }, 'cs').columns.find((c) => c.id === id);
    if (!column) throw new Error(`${id} column missing`);
    return column;
  };

  it('names the pay period by its days in the language of the session, not by the English label', () => {
    const value = columnOf('payPeriodLabel').getValue?.(invoice({}));
    expect(value).toBe('1. 11. 2026 – 14. 11. 2026');
    expect(value).not.toContain('Nov');
  });

  // The server sorts invoices by neither and falls back to the generation date (EmployeeInvoiceSort).
  it('offers no sort on the pay period or the order count', () => {
    expect(columnOf('payPeriodLabel').sortable).toBeFalsy();
    expect(columnOf('totalOrders').sortable).toBeFalsy();
  });

  it('shows the cash set off and the bank transfer the server computed, in the invoice currency, unsorted', () => {
    const row = invoice({ totalAmount: 1250.5, cashSetOffAmount: 301, transferAmount: 949.5, currencyCode: 'CZK' });
    expect(columnOf('cashSetOffAmount').getValue?.(row)).toBe('301,00\u00a0Kč');
    expect(columnOf('transferAmount').getValue?.(row)).toBe('949,50\u00a0Kč');
    expect(columnOf('cashSetOffAmount').sortable).toBeFalsy();
    expect(columnOf('transferAmount').sortable).toBeFalsy();
  });
});
