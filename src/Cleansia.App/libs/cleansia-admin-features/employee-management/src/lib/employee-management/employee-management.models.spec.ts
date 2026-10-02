import { AdminEmployeeListItem, ContractStatus } from '@cleansia/admin-services';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { getEmployeeTableDefinition } from './employee-management.models';

describe('employee-management models', () => {
  const translate = { instant: (k: string) => k, currentLang: 'en' } as unknown as TranslateService;

  function row(status: ContractStatus, isProfileComplete = true): AdminEmployeeListItem {
    return AdminEmployeeListItem.fromJS({
      id: 'emp-1',
      contractStatus: ContractStatus[status],
      isProfileComplete,
    });
  }

  function action(tooltip: string, allowed: string[] = [Policy.CanApproveEmployee, Policy.CanRejectEmployee]) {
    const permissions = {
      hasPolicy: (policy: string) => allowed.includes(policy),
    } as unknown as PermissionService;
    const found = getEmployeeTableDefinition(
      { onApprove: jest.fn(), onReject: jest.fn(), onViewDetails: jest.fn() },
      translate,
      permissions
    ).actions.find((a) => a.tooltip === tooltip);
    if (!found) throw new Error(`no action ${tooltip}`);
    return found;
  }

  const approve = (allowed?: string[]) => action('pages.employee_management.approve', allowed);
  const reject = (allowed?: string[]) => action('pages.employee_management.reject', allowed);

  it('offers both decisions on a pending cleaner with a complete profile', () => {
    expect(approve().visible?.(row(ContractStatus.Pending))).toBe(true);
    expect(reject().visible?.(row(ContractStatus.Pending))).toBe(true);
  });

  it('offers Approve but not Reject on a rejected cleaner', () => {
    expect(approve().visible?.(row(ContractStatus.Rejected))).toBe(true);
    expect(reject().visible?.(row(ContractStatus.Rejected))).toBe(false);
  });

  it('offers neither on an approved cleaner', () => {
    expect(approve().visible?.(row(ContractStatus.Approved))).toBe(false);
    expect(reject().visible?.(row(ContractStatus.Approved))).toBe(false);
  });

  it('offers neither while the profile is incomplete', () => {
    expect(approve().visible?.(row(ContractStatus.Rejected, false))).toBe(false);
    expect(reject().visible?.(row(ContractStatus.Pending, false))).toBe(false);
  });

  it('withholds each decision from a role that lacks its policy', () => {
    expect(approve([Policy.CanRejectEmployee]).visible?.(row(ContractStatus.Rejected))).toBe(false);
    expect(reject([Policy.CanApproveEmployee]).visible?.(row(ContractStatus.Pending))).toBe(false);
  });
});
