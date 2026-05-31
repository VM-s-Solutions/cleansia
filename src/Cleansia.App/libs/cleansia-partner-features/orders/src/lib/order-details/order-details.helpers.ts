import { TranslateService } from '@ngx-translate/core';
import { toSnakeCase } from '@cleansia/utils';
import { AssignedEmployeeDto, OrderStatus } from '@cleansia/partner-services';

// --- Formatting helpers ---

export function formatCurrency(amount: number, currencySymbol: string): string {
  return `${amount.toLocaleString('en-GB', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })} ${currencySymbol}`;
}

export function formatDate(date: string | Date | undefined): string {
  if (!date) return '';
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  const day = dateObj.getDate().toString().padStart(2, '0');
  const month = (dateObj.getMonth() + 1).toString().padStart(2, '0');
  const year = dateObj.getFullYear();
  return `${day}.${month}.${year}`;
}

export function formatDateTime(date: string | Date | undefined): string {
  if (!date) return '';
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  const day = dateObj.getDate().toString().padStart(2, '0');
  const month = (dateObj.getMonth() + 1).toString().padStart(2, '0');
  const year = dateObj.getFullYear();
  const hours = dateObj.getHours().toString().padStart(2, '0');
  const minutes = dateObj.getMinutes().toString().padStart(2, '0');
  return `${day}.${month}.${year} ${hours}:${minutes}`;
}

export function formatAddress(address: {
  street: string;
  city: string;
  zipCode: string;
  country: string;
} | null | undefined): string {
  if (!address) return '';
  return `${address.street}, ${address.city}, ${address.zipCode}, ${address.country}`;
}

// --- Translation helpers ---

export function translateEnum(
  translateService: TranslateService,
  enumType: string,
  name: string | undefined
): string {
  if (!name) return '';
  const translationKey = `enums.${enumType}.${toSnakeCase(name)}`;
  const translatedLabel = translateService.instant(translationKey);
  return translatedLabel !== translationKey ? translatedLabel : name;
}

export function buildTranslatedOption(
  translateService: TranslateService,
  enumType: string,
  enumObj: { name?: string } | undefined
): { label: string; value: string }[] {
  if (!enumObj?.name) return [];
  const label = translateEnum(translateService, enumType, enumObj.name);
  return [{ label, value: enumObj.name }];
}

// --- Status history helpers ---

const STATUS_CLASS_MAP: Record<number, string> = {
  1: 'status-pending',
  2: 'status-confirmed',
  3: 'status-inprogress',
  4: 'status-completed',
  5: 'status-cancelled',
  // OnTheWay = 6 — between Confirmed and InProgress in workflow but appended
  // numerically. See backend OrderStatus.cs for why the value isn't slotted
  // between 2 and 3.
  6: 'status-ontheway',
};

const STATUS_ICON_MAP: Record<number, string> = {
  1: 'pi pi-clock',
  2: 'pi pi-check',
  3: 'pi pi-spinner',
  4: 'pi pi-check-circle',
  5: 'pi pi-times-circle',
  6: 'pi pi-send',
};

export function getStatusHistoryClass(statusValue: number | undefined): string {
  const suffix = STATUS_CLASS_MAP[statusValue ?? 0] ?? 'status-pending';
  return `status-history-item ${suffix}`;
}

export function getStatusHistoryIcon(statusValue: number | undefined): string {
  return STATUS_ICON_MAP[statusValue ?? 0] ?? 'pi pi-circle';
}

// --- Order state helpers ---

export function isEmployeeAssigned(
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  return assignedEmployees?.some((e) => e?.employeeId === employeeId) ?? false;
}

export function canTakeOrder(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  const isPendingOrConfirmed = orderStatusValue === OrderStatus.Pending || orderStatusValue === OrderStatus.Confirmed;
  return isPendingOrConfirmed && !isEmployeeAssigned(assignedEmployees, employeeId);
}

export function canStartOrder(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  const isReadyToStart = orderStatusValue === OrderStatus.Confirmed || orderStatusValue === OrderStatus.OnTheWay;
  return isReadyToStart && isEmployeeAssigned(assignedEmployees, employeeId);
}

export function canCompleteOrder(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  return orderStatusValue === OrderStatus.InProgress && isEmployeeAssigned(assignedEmployees, employeeId);
}

// Photo section is visible (read or write) when employee is assigned and the order
// has progressed past acceptance — Confirmed, OnTheWay, InProgress, or Completed.
export function canManagePhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  const isPhotoEligibleStatus =
    orderStatusValue === OrderStatus.Confirmed ||
    orderStatusValue === OrderStatus.OnTheWay ||
    orderStatusValue === OrderStatus.InProgress ||
    orderStatusValue === OrderStatus.Completed;
  return isPhotoEligibleStatus && isEmployeeAssigned(assignedEmployees, employeeId);
}

export function canUploadPhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  return orderStatusValue === OrderStatus.InProgress && isEmployeeAssigned(assignedEmployees, employeeId);
}

// Before photos: allowed during Confirmed or OnTheWay (preparation phase).
export function canUploadBeforePhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  const isPreparationPhase =
    orderStatusValue === OrderStatus.Confirmed || orderStatusValue === OrderStatus.OnTheWay;
  return isPreparationPhase && isEmployeeAssigned(assignedEmployees, employeeId);
}

// After photos: allowed only during InProgress (active work phase).
export function canUploadAfterPhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  return orderStatusValue === OrderStatus.InProgress && isEmployeeAssigned(assignedEmployees, employeeId);
}

// Notes / issues: allowed for any active order status (Confirmed, OnTheWay, InProgress),
// gated OUT of Completed and Cancelled.
export function canAddNoteOrIssue(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  const isActive =
    orderStatusValue === OrderStatus.Confirmed ||
    orderStatusValue === OrderStatus.OnTheWay ||
    orderStatusValue === OrderStatus.InProgress;
  return isActive && isEmployeeAssigned(assignedEmployees, employeeId);
}

// Completion requires InProgress AND at least one After photo present.
export function canCompleteOrderWithPhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string,
  hasAfterPhotos: boolean
): boolean {
  return canCompleteOrder(orderStatusValue, assignedEmployees, employeeId) && hasAfterPhotos;
}

export function computeElapsedTime(
  orderStatusValue: number,
  statusHistory: { status: { value: number }; createdOn: string | Date }[] | undefined
): { hours: number; minutes: number } | null {
  if (orderStatusValue !== OrderStatus.InProgress) return null;
  const startEntry = statusHistory?.find((h) => h.status.value === OrderStatus.InProgress);
  if (!startEntry) return null;
  const start = new Date(startEntry.createdOn);
  const elapsed = Math.floor((Date.now() - start.getTime()) / 60000);
  return { hours: Math.floor(elapsed / 60), minutes: elapsed % 60 };
}

export function buildCurrencyOptions(
  currency: { name?: string; code?: string } | null | undefined
): { label: string; value: string }[] {
  if (!currency) return [];
  const display = `${currency.name} (${currency.code})`;
  return [{ label: display, value: display }];
}

export function hasExtras(extras: Record<string, boolean> | undefined): boolean {
  return !!extras && Object.entries(extras).some(([_, value]) => value);
}

export function getExtrasEntries(
  extras: Record<string, boolean> | undefined
): [string, boolean][] {
  return extras
    ? (Object.entries(extras).filter(([_, value]) => value) as [string, boolean][])
    : [];
}
