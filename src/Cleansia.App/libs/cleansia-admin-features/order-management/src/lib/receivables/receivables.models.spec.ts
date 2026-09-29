import { ReceivableKind, ReceivableListItem, ReceivableStatus } from '@cleansia/admin-services';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { getReceivablesTableDefinition } from './receivables.models';

describe('receivables table', () => {
  const translate = { instant: (key: string) => key, currentLang: 'cs' } as unknown as TranslateService;

  function row(status: ReceivableStatus, kind = ReceivableKind.CashCancellationFee): ReceivableListItem {
    return ReceivableListItem.fromJS({
      id: 'receivable-1',
      orderId: 'order-1',
      kind: { value: kind, name: ReceivableKind[kind] },
      status: { value: status, name: ReceivableStatus[status] },
      amount: 450,
      currencyCode: 'CZK',
    });
  }

  function definition(allowed: string[]) {
    const permissions = { hasPolicy: (policy: string) => allowed.includes(policy) } as unknown as PermissionService;
    return getReceivablesTableDefinition(
      { onViewOrder: jest.fn(), onWriteOff: jest.fn(), isBusy: () => false },
      translate,
      permissions
    );
  }

  const writeOffAction = (allowed: string[]) =>
    definition(allowed).actions.find((action) => action.tooltip === 'pages.receivables.actions.write_off')!;

  it('offers the write-off only on an open receivable and only to a role that may write one off', () => {
    const permitted = writeOffAction([Policy.CanWriteOffReceivable]);
    const refused = writeOffAction([]);

    expect(permitted.visible?.(row(ReceivableStatus.Open))).toBe(true);
    expect(permitted.visible?.(row(ReceivableStatus.Paid))).toBe(false);
    expect(permitted.visible?.(row(ReceivableStatus.WrittenOff))).toBe(false);
    expect(refused.visible?.(row(ReceivableStatus.Open))).toBe(false);
  });

  it('names the kind and the status through the enum copy, and prints the amount in its currency', () => {
    const columns = definition([]).columns;
    const value = (id: string, item: ReceivableListItem) => columns.find((c) => c.id === id)?.getValue?.(item);
    const lockout = row(ReceivableStatus.WrittenOff, ReceivableKind.Lockout);

    expect(value('kind', lockout)).toBe('enums.receivable_kind.lockout');
    expect(value('status', lockout)).toBe('enums.receivable_status.written_off');
    expect(value('amount', lockout)).toBe('450,00 Kč');
  });
});
