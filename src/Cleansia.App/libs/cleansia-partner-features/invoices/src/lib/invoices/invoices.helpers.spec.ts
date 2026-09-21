import { EmployeeInvoiceStatus } from '@cleansia/partner-services';
import { resolveStatusBadge } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';
import { buildInvoiceStatusOptions, INVOICE_STATUS_FLOW } from './invoices.helpers';

describe('invoice status helpers', () => {
  const translate = { instant: (key: string) => key } as unknown as TranslateService;

  it('offers every member of the generated enum as a filter, labelled from the enum namespace', () => {
    const members = Object.values(EmployeeInvoiceStatus).filter(
      (value): value is EmployeeInvoiceStatus => typeof value === 'number',
    );
    const options = buildInvoiceStatusOptions(translate);
    expect(options.map((o) => o.value)).toEqual(members);
    for (const option of options) {
      expect(option.label).toMatch(/^enums\.invoice_status\.[a-z_]+$/);
    }
  });

  // The legend beside the table explains the same pills the table draws, so its tones come from
  // the shared badge catalogue rather than a second ramp.
  it('colours the help legend with the shared badge tone of each status', () => {
    expect(INVOICE_STATUS_FLOW.length).toBe(6);
    for (const item of INVOICE_STATUS_FLOW) {
      const member = item.statusKey.replace('enums.invoice_status.', '');
      const tone = resolveStatusBadge('invoice', member)?.tone;
      expect(tone).toBeDefined();
      expect(item.colorClass).toBe(`status-badge status-badge--${tone}`);
    }
  });
});
