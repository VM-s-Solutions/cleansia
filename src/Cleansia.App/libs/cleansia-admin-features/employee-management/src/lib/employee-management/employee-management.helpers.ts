import { AdminEmployeeListItem, ContractStatus } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';

// --- Contract status helpers ---

export function getContractStatusClass(employee: AdminEmployeeListItem): string {
  const statusName =
    employee.contractStatus?.toLowerCase().replace(/\s+/g, '-') || 'pending';
  return `contract-status-badge status-${statusName}`;
}

export function getContractStatusLabel(
  employee: AdminEmployeeListItem,
  translate: TranslateService
): string {
  if (!employee.contractStatus) return '';
  const statusKey = employee.contractStatus.toLowerCase();
  return translate.instant(
    `pages.employee_management.contract_status.${statusKey}`
  );
}

// --- Filter option builders ---

export function buildContractStatusOptions(
  translate: TranslateService
): { label: string; value: ContractStatus }[] {
  return [
    ContractStatus.Pending,
    ContractStatus.Active,
    ContractStatus.Approved,
    ContractStatus.Rejected,
    ContractStatus.Terminated,
  ].map((status) => ({
    label: translate.instant(
      `pages.employee_management.contract_status.${ContractStatus[status].toLowerCase()}`
    ),
    value: status,
  }));
}

export function buildActiveStatusOptions(
  translate: TranslateService
): { label: string; value: boolean }[] {
  return [
    { label: translate.instant('global.status.active'), value: true },
    { label: translate.instant('global.status.inactive'), value: false },
  ];
}

// --- Filter chip types & logic ---

export interface FilterChip {
  key: string;
  label: string;
  value: string;
}

export interface EmployeeFilterValues {
  searchTerm?: string | null;
  contractStatus?: ContractStatus[] | null;
  isActive?: boolean | null;
}

export function buildFilterChips(
  values: EmployeeFilterValues,
  contractStatusOptions: { label: string; value: ContractStatus }[],
  activeStatusOptions: { label: string; value: boolean }[],
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];

  if (values.searchTerm) {
    chips.push({
      key: 'searchTerm',
      label: translate.instant('pages.employee_management.filters.search'),
      value: values.searchTerm,
    });
  }

  if (values.contractStatus && values.contractStatus.length > 0) {
    const statusLabels = values.contractStatus
      .map((s) => contractStatusOptions.find((o) => o.value === s)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'contractStatus',
      label: translate.instant(
        'pages.employee_management.filters.contract_status'
      ),
      value: statusLabels,
    });
  }

  if (values.isActive !== null && values.isActive !== undefined) {
    const activeLabel = activeStatusOptions.find(
      (o) => o.value === values.isActive
    )?.label;
    if (activeLabel) {
      chips.push({
        key: 'isActive',
        label: translate.instant(
          'pages.employee_management.filters.active_status'
        ),
        value: activeLabel,
      });
    }
  }

  return chips;
}

// --- Contract status checkbox helpers ---

export function toggleContractStatusInList(
  currentStatuses: ContractStatus[],
  status: ContractStatus,
  checked: boolean
): ContractStatus[] {
  const result = [...currentStatuses];
  if (checked) {
    if (!result.includes(status)) {
      result.push(status);
    }
  } else {
    const index = result.indexOf(status);
    if (index > -1) {
      result.splice(index, 1);
    }
  }
  return result;
}
