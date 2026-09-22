import { EmployeeInvoiceDto, EmployeeInvoiceStatus, IEmployeeInvoiceDto } from '@cleansia/partner-services';
import { getInvoicesTableDefinition } from './invoices.models';

function invoice(overrides: Partial<IEmployeeInvoiceDto>): EmployeeInvoiceDto {
  return EmployeeInvoiceDto.fromJS({
    id: 'inv-1',
    employeeId: 'emp-1',
    employeeName: 'Jana',
    payPeriodId: 'pp-1',
    payPeriodLabel: 'W36',
    invoiceNumber: 'INV-1',
    variableSymbol: '1',
    totalOrders: 1,
    subTotal: 1200,
    bonusAmount: 0,
    deductionAmount: 0,
    totalAmount: 1200,
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
