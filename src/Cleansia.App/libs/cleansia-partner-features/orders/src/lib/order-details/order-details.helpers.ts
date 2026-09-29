import { TranslateService } from '@ngx-translate/core';
import { cashIsRefused, resolveCashEligibility } from '@cleansia/models';
import { formatDate as formatSharedDate, formatMoney, localeFor, toSnakeCase } from '@cleansia/utils';
import {
  AssignedEmployeeDto,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  WorkContractAcceptanceDto,
} from '@cleansia/partner-services';

// --- Formatting helpers ---

export function formatCurrency(
  amount: number,
  currencyCode: string | null | undefined,
  lang: string | undefined
): string {
  return formatMoney(amount, currencyCode, localeFor(lang), { fractionDigits: 2 });
}

export function formatDate(date: string | Date | undefined, lang: string | undefined): string {
  return formatSharedDate(date, lang);
}

export function formatDateTime(date: string | Date | undefined, lang: string | undefined): string {
  return formatSharedDate(date, lang, 'dateTime');
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
  // Mirrors OrderAvailability.OfferableStatuses. Started is not over (owner ruling 2026-09-06).
  const isOfferable =
    orderStatusValue === OrderStatus.New ||
    orderStatusValue === OrderStatus.Confirmed ||
    orderStatusValue === OrderStatus.OnTheWay ||
    orderStatusValue === OrderStatus.InProgress;
  return isOfferable && !isEmployeeAssigned(assignedEmployees, employeeId);
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

// Mirrors OrderPhoto.MayBeAddedAt: a before photo from the take until the work is done, an after
// photo only while it is in progress.
export function canUploadBeforePhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  const isBeforeWindow =
    orderStatusValue === OrderStatus.Confirmed ||
    orderStatusValue === OrderStatus.OnTheWay ||
    orderStatusValue === OrderStatus.InProgress;
  return isBeforeWindow && isEmployeeAssigned(assignedEmployees, employeeId);
}

export function canUploadAfterPhotos(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  employeeId: string
): boolean {
  return orderStatusValue === OrderStatus.InProgress && isEmployeeAssigned(assignedEmployees, employeeId);
}

// The server answers a crew member with no customer and no address once their access has ended: 24
// hours after completion, at once on cancellation. A live job always carries its address.
export function customerDetailsClosedNoticeKey(order: OrderItem, employeeId: string): string | null {
  if (order.address || !isEmployeeAssigned(order.assignedEmployees, employeeId)) return null;
  return order.orderStatus?.value === OrderStatus.Cancelled
    ? 'pages.order_details.customer_details_closed_cancelled'
    : 'pages.order_details.customer_details_closed_completed';
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

// A card order is refused cash unless its booking could have been paid in cash. The crew is on the
// job sheet; whether the customer was a guest is not, so that half of the rule is the server's alone.
// An order booked as cash stays collectable whatever its crew.
export function cashRefusedOnCardOrder(
  paymentTypeValue: number | undefined,
  requiredEmployees: number | undefined
): boolean {
  return (
    paymentTypeValue !== PaymentType.Cash &&
    cashIsRefused(resolveCashEligibility(true, requiredEmployees ?? null))
  );
}

// The server's refusals the job sheet already shows, as the sentence the cleaner is given. A
// credit-bearing order owes less than the total the confirmation would ask the cleaner to take.
export function cashCollectionRefusal(order: OrderItem): string | null {
  if (order.creditAppliedAmount > 0) return 'api.credit.cash_not_collectable_on_credit_order';
  if (cashRefusedOnCardOrder(order.paymentType?.value, order.requiredEmployees)) {
    return 'api.order.cash_not_allowed_on_card_order';
  }
  return null;
}

// Cash changes hands only while the cleaner is on site (InProgress), on an order still awaiting
// payment (a refunded or disputed one is not), to a cleaner on its crew. A one-cleaner card order
// stays offered: the server first checks Stripe, so one whose webhook never arrived is repaired to
// Paid, and refuses a guest's itself.
export function canMarkCashCollected(order: OrderItem, employeeId: string): boolean {
  const paymentStatus = order.paymentStatus?.value;
  return (
    order.orderStatus?.value === OrderStatus.InProgress &&
    (paymentStatus === PaymentStatus.Pending || paymentStatus === PaymentStatus.Failed) &&
    cashCollectionRefusal(order) === null &&
    isEmployeeAssigned(order.assignedEmployees, employeeId)
  );
}

// The caller's contract for work is the row of their own seat: the acceptance names the seat,
// the crew entry names the cleaner, and the two meet on the seat id.
export function findCallerWorkContractAcceptance(
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  workContractAcceptances: WorkContractAcceptanceDto[] | undefined,
  employeeId: string
): WorkContractAcceptanceDto | null {
  const seatId = assignedEmployees?.find((e) => e?.employeeId === employeeId)?.id;
  if (!seatId) return null;
  return workContractAcceptances?.find((a) => a?.orderEmployeeId === seatId) ?? null;
}

// A seat without a row is the admin placement; the standalone acceptance mirrors
// AcceptWorkContract.Validator — any order that is not over.
export function canAcceptWorkContract(
  orderStatusValue: number,
  assignedEmployees: AssignedEmployeeDto[] | undefined,
  workContractAcceptances: WorkContractAcceptanceDto[] | undefined,
  employeeId: string
): boolean {
  const isOver =
    orderStatusValue === OrderStatus.Completed || orderStatusValue === OrderStatus.Cancelled;
  return (
    !isOver &&
    isEmployeeAssigned(assignedEmployees, employeeId) &&
    findCallerWorkContractAcceptance(assignedEmployees, workContractAcceptances, employeeId) === null
  );
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
  return !!extras && Object.values(extras).some((value) => value);
}

export function getExtrasEntries(
  extras: Record<string, boolean> | undefined
): [string, boolean][] {
  return extras
    ? Object.entries(extras).filter(([, value]) => value)
    : [];
}
