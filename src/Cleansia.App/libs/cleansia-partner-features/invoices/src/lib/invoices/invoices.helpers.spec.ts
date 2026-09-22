import { EmployeeInvoiceDto, EmployeeInvoiceStatus } from '@cleansia/partner-services';
import { getInvoiceStatusClass, getInvoiceStatusLabelKey } from './invoices.helpers';

describe('invoice status helpers', () => {
  const withStatus = (status: EmployeeInvoiceStatus): EmployeeInvoiceDto =>
    EmployeeInvoiceDto.fromJS({ id: 'inv-1', status });

  it.each([
    [EmployeeInvoiceStatus.Pending, 'pending'],
    [EmployeeInvoiceStatus.Approved, 'approved'],
    [EmployeeInvoiceStatus.Paid, 'paid'],
    [EmployeeInvoiceStatus.Disputed, 'disputed'],
    [EmployeeInvoiceStatus.Rejected, 'rejected'],
    [EmployeeInvoiceStatus.Cancelled, 'cancelled'],
  ])('names status %s as %s for the badge class and the label key', (status, name) => {
    expect(getInvoiceStatusClass(withStatus(status))).toBe(`status-badge status-${name}`);
    expect(getInvoiceStatusLabelKey(withStatus(status))).toBe(`pages.invoices.status_${name}`);
  });

  it('covers every member of the generated enum', () => {
    const members = Object.values(EmployeeInvoiceStatus).filter(
      (value): value is EmployeeInvoiceStatus => typeof value === 'number',
    );
    expect(members.length).toBe(6);
    const names = new Set(members.map((status) => getInvoiceStatusLabelKey(withStatus(status))));
    expect(names.size).toBe(members.length);
  });
});
