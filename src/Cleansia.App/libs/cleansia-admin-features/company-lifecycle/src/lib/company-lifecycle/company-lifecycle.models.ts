import { TemplateRef } from '@angular/core';
import { CompanyLifecycleDto, CompanyLifecycleState, WindDownCompanyCommand } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { CleansiaAdminRoute, Policy, PolicyName } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

const PAGE = 'pages.company_lifecycle';

export enum LifecycleAct {
  Deactivate = 'deactivate',
  Reactivate = 'reactivate',
  WindDown = 'wind_down',
  Archive = 'archive',
}

export type SettlementFactId =
  | 'openOrders'
  | 'openOrdersOnOrAfterWindDownFrom'
  | 'activeTemplates'
  | 'activeMemberships'
  | 'creditBalances'
  | 'pendingRefunds'
  | 'ordersAwaitingPay'
  | 'ordersAwaitingReceipt'
  | 'receiptsAwaitingFiscalRegistration'
  | 'openPayPeriods'
  | 'unpaidInvoices'
  | 'uninvoicedPayRows'
  | 'openDisputes'
  | 'chargebackHorizonEndsOn'
  | 'windDownLastRunOn'
  | 'windDownRunStartedOn';

export type SettlementFactKind = 'count' | 'date';
export type SettlementFactStatus = 'blocking' | 'settled' | 'informational';
export type StateSeverity = 'success' | 'warn' | 'danger' | 'info' | 'secondary';

export interface SettlementFactDefinition {
  id: SettlementFactId;
  kind: SettlementFactKind;
  archivePrecondition: boolean;
  route: string | null;
}

export interface SettlementFact {
  id: SettlementFactId;
  kind: SettlementFactKind;
  count: number;
  date: Date | null;
  status: SettlementFactStatus;
  route: string | null;
}

export interface SettlementFactRow extends SettlementFact {
  display: string;
  statusKey: string;
}

export interface ActReason {
  key: string;
  params?: Record<string, unknown>;
}

export interface ActAvailability {
  act: LifecycleAct;
  policy: PolicyName;
  labelKey: string;
  icon: string;
  severity: 'primary' | 'secondary' | 'danger';
  /** The one filled act is the recovering one; the rest are outlined, danger in red. */
  outlined: boolean;
  enabled: boolean;
  reasons: ActReason[];
}

export interface LifecycleStamp {
  key: string;
  params: Record<string, string>;
}

const ORDERS = `/${CleansiaAdminRoute.ORDER_MANAGEMENT}`;
const PAY_PERIODS = `/${CleansiaAdminRoute.PAY_PERIODS}`;

// The one-hour staleness the server applies to `WindDownRunStartedOn`: a run that crashed after its
// first commit must not lock the re-run button for ever.
export const WIND_DOWN_RUN_STALE_AFTER_MS = 60 * 60 * 1000;

export const SETTLEMENT_FACT_DEFINITIONS: readonly SettlementFactDefinition[] = [
  { id: 'openOrders', kind: 'count', archivePrecondition: true, route: ORDERS },
  { id: 'openOrdersOnOrAfterWindDownFrom', kind: 'count', archivePrecondition: false, route: ORDERS },
  { id: 'activeTemplates', kind: 'count', archivePrecondition: false, route: null },
  { id: 'activeMemberships', kind: 'count', archivePrecondition: true, route: null },
  { id: 'creditBalances', kind: 'count', archivePrecondition: true, route: null },
  { id: 'pendingRefunds', kind: 'count', archivePrecondition: true, route: null },
  { id: 'ordersAwaitingPay', kind: 'count', archivePrecondition: true, route: ORDERS },
  { id: 'ordersAwaitingReceipt', kind: 'count', archivePrecondition: true, route: ORDERS },
  { id: 'receiptsAwaitingFiscalRegistration', kind: 'count', archivePrecondition: true, route: null },
  { id: 'openPayPeriods', kind: 'count', archivePrecondition: true, route: PAY_PERIODS },
  { id: 'unpaidInvoices', kind: 'count', archivePrecondition: true, route: `/${CleansiaAdminRoute.INVOICE_MANAGEMENT}` },
  { id: 'uninvoicedPayRows', kind: 'count', archivePrecondition: true, route: PAY_PERIODS },
  { id: 'openDisputes', kind: 'count', archivePrecondition: true, route: `/${CleansiaAdminRoute.DISPUTE_MANAGEMENT}` },
  { id: 'chargebackHorizonEndsOn', kind: 'date', archivePrecondition: true, route: `/${CleansiaAdminRoute.COMPANY_SETTINGS}` },
  { id: 'windDownLastRunOn', kind: 'date', archivePrecondition: false, route: null },
  { id: 'windDownRunStartedOn', kind: 'date', archivePrecondition: false, route: null },
];

export function getFactNameKey(id: SettlementFactId): string {
  return `${PAGE}.facts.${id}`;
}

export function getStateSeverity(state: CompanyLifecycleState): StateSeverity {
  switch (state) {
    case CompanyLifecycleState.Operating:
      return 'success';
    case CompanyLifecycleState.WindingDown:
      return 'warn';
    case CompanyLifecycleState.Deactivated:
      return 'danger';
    case CompanyLifecycleState.Frozen:
      return 'info';
    case CompanyLifecycleState.Archived:
      return 'secondary';
  }
}

export function getFactStatusKey(status: SettlementFactStatus): string {
  return `${PAGE}.status.${status}`;
}

export function startOfDay(instant: Date): Date {
  return new Date(instant.getFullYear(), instant.getMonth(), instant.getDate());
}

export function isWindDownRunInProgress(dto: CompanyLifecycleDto, now: Date): boolean {
  const started = dto.windDownRunStartedOn;
  return !!started && now.getTime() - started.getTime() < WIND_DOWN_RUN_STALE_AFTER_MS;
}

function isHorizonAhead(dto: CompanyLifecycleDto, now: Date): boolean {
  return !!dto.chargebackHorizonEndsOn && dto.chargebackHorizonEndsOn.getTime() > now.getTime();
}

function isFrozenOrArchived(state: CompanyLifecycleState): boolean {
  return state === CompanyLifecycleState.Frozen || state === CompanyLifecycleState.Archived;
}

export function buildSettlementFacts(dto: CompanyLifecycleDto, now: Date): SettlementFact[] {
  return SETTLEMENT_FACT_DEFINITIONS.map((definition) => {
    const value = dto[definition.id];
    const count = typeof value === 'number' ? value : 0;
    const date = value instanceof Date ? value : null;
    return {
      id: definition.id,
      kind: definition.kind,
      count,
      date,
      status: factStatus(definition, count, dto, now),
      route: definition.route,
    };
  });
}

function factStatus(
  definition: SettlementFactDefinition,
  count: number,
  dto: CompanyLifecycleDto,
  now: Date
): SettlementFactStatus {
  if (!definition.archivePrecondition) return 'informational';
  if (definition.id === 'chargebackHorizonEndsOn') return isHorizonAhead(dto, now) ? 'blocking' : 'settled';
  return count > 0 ? 'blocking' : 'settled';
}

export function formatSettlementFactValue(
  fact: SettlementFact,
  translate: TranslateService,
  formatDay: (date: Date) => string
): string {
  if (fact.kind === 'count') return String(fact.count);
  if (fact.id === 'chargebackHorizonEndsOn') {
    return fact.date
      ? translate.instant(`${PAGE}.values.archive_admissible_from`, { date: formatDay(fact.date) })
      : translate.instant(`${PAGE}.values.no_horizon`);
  }
  if (fact.date) return formatDay(fact.date);
  return translate.instant(fact.id === 'windDownRunStartedOn' ? `${PAGE}.values.not_running` : `${PAGE}.values.never`);
}

export function getActAvailability(
  dto: CompanyLifecycleDto,
  now: Date,
  factName: (id: SettlementFactId) => string,
  formatDay: (date: Date) => string
): ActAvailability[] {
  const windDownRequested = !!dto.windDownFrom;
  return [
    {
      act: LifecycleAct.Deactivate,
      policy: Policy.CanDeactivateCompany,
      labelKey: `${PAGE}.acts.deactivate`,
      icon: 'pi pi-power-off',
      severity: 'danger',
      outlined: true,
      ...withReasons(deactivateReasons(dto)),
    },
    {
      act: LifecycleAct.Reactivate,
      policy: Policy.CanReactivateCompany,
      labelKey: `${PAGE}.acts.reactivate`,
      icon: 'pi pi-replay',
      severity: 'primary',
      outlined: false,
      ...withReasons(reactivateReasons(dto)),
    },
    {
      act: LifecycleAct.WindDown,
      policy: Policy.CanWindDownCompany,
      labelKey: windDownRequested ? `${PAGE}.acts.run_wind_down_again` : `${PAGE}.acts.wind_down`,
      icon: 'pi pi-calendar-times',
      severity: 'secondary',
      outlined: true,
      ...withReasons(windDownReasons(dto, now)),
    },
    {
      act: LifecycleAct.Archive,
      policy: Policy.CanArchiveCompany,
      labelKey: dto.state === CompanyLifecycleState.Frozen ? `${PAGE}.acts.build_archive_again` : `${PAGE}.acts.archive`,
      icon: 'pi pi-lock',
      severity: 'secondary',
      outlined: true,
      ...withReasons(archiveReasons(dto, now, factName, formatDay)),
    },
  ];
}

function withReasons(reasons: ActReason[]): Pick<ActAvailability, 'enabled' | 'reasons'> {
  return { enabled: reasons.length === 0, reasons };
}

function deactivateReasons(dto: CompanyLifecycleDto): ActReason[] {
  if (isFrozenOrArchived(dto.state)) return [{ key: 'api.company.archived' }];
  if (dto.state === CompanyLifecycleState.Deactivated) return [{ key: 'api.company.already_deactivated' }];
  if (dto.operatesDefaultMarket) return [{ key: 'api.company.operates_default_market' }];
  return [];
}

function reactivateReasons(dto: CompanyLifecycleDto): ActReason[] {
  if (isFrozenOrArchived(dto.state)) return [{ key: 'api.company.archived' }];
  if (dto.state !== CompanyLifecycleState.Deactivated) return [{ key: 'api.company.not_deactivated' }];
  return [];
}

function windDownReasons(dto: CompanyLifecycleDto, now: Date): ActReason[] {
  if (isFrozenOrArchived(dto.state)) return [{ key: 'api.company.archived' }];
  if (isWindDownRunInProgress(dto, now)) return [{ key: 'api.company.wind_down_in_progress' }];
  return [];
}

function archiveReasons(
  dto: CompanyLifecycleDto,
  now: Date,
  factName: (id: SettlementFactId) => string,
  formatDay: (date: Date) => string
): ActReason[] {
  if (dto.state === CompanyLifecycleState.Archived) return [{ key: 'api.company.archived' }];
  if (dto.state === CompanyLifecycleState.Frozen) return [];

  const reasons: ActReason[] = [];
  if (dto.state !== CompanyLifecycleState.Deactivated) reasons.push({ key: 'api.company.not_deactivated' });
  if (!dto.windDownFrom) reasons.push({ key: 'api.company.wind_down_not_requested' });

  const unsettled = buildSettlementFacts(dto, now).filter((fact) => fact.kind === 'count' && fact.status === 'blocking');
  if (unsettled.length > 0) {
    reasons.push({
      key: `${PAGE}.reasons.unsettled_facts`,
      params: { facts: unsettled.map((fact) => `${factName(fact.id)} (${fact.count})`).join(', ') },
    });
  }
  if (dto.chargebackHorizonEndsOn && isHorizonAhead(dto, now)) {
    reasons.push({ key: `${PAGE}.reasons.within_chargeback_horizon`, params: { date: formatDay(dto.chargebackHorizonEndsOn) } });
  }
  return reasons;
}

export function buildStamps(
  dto: CompanyLifecycleDto,
  formatDay: (date: Date) => string,
  formatStamp: (date: Date) => string
): LifecycleStamp[] {
  const stamps: LifecycleStamp[] = [];
  if (dto.windDownFrom && dto.windDownRequestedOn) {
    stamps.push({
      key: `${PAGE}.stamps.wind_down`,
      params: {
        from: formatDay(dto.windDownFrom),
        requestedOn: formatStamp(dto.windDownRequestedOn),
        email: dto.windDownRequestedByEmail ?? '',
      },
    });
  }
  if (dto.deactivatedOn) {
    stamps.push({
      key: `${PAGE}.stamps.deactivated`,
      params: { date: formatStamp(dto.deactivatedOn), email: dto.deactivatedByEmail ?? '' },
    });
  }
  if (dto.archiveRequestedOn) {
    stamps.push({
      key: `${PAGE}.stamps.frozen`,
      params: { date: formatStamp(dto.archiveRequestedOn), email: dto.archiveRequestedByEmail ?? '' },
    });
  }
  if (dto.archivedOn) {
    stamps.push({ key: `${PAGE}.stamps.archived`, params: { date: formatStamp(dto.archivedOn) } });
  }
  return stamps;
}

export function buildWindDownCommand(fromDate: Date | undefined): WindDownCompanyCommand {
  const command = new WindDownCompanyCommand();
  command.fromDate = fromDate;
  return command;
}

export function getSettlementFactsTableDefinition(
  translate: TranslateService,
  valueTemplate?: TemplateRef<SettlementFactRow>,
  statusTemplate?: TemplateRef<SettlementFactRow>
): { columns: TableColumn<SettlementFactRow>[]; actions: TableAction<SettlementFactRow>[] } {
  return {
    columns: [
      {
        id: 'fact',
        field: 'id',
        header: translate.instant(`${PAGE}.columns.fact`),
        getValue: (row: SettlementFactRow) => translate.instant(getFactNameKey(row.id)),
        width: '50%',
      },
      {
        id: 'value',
        numeric: true,
        field: 'display',
        header: translate.instant(`${PAGE}.columns.value`),
        getValue: (row: SettlementFactRow) => row.display,
        customTemplate: valueTemplate,
        width: '30%',
      },
      {
        id: 'status',
        field: 'status',
        header: translate.instant(`${PAGE}.columns.status`),
        getValue: (row: SettlementFactRow) => translate.instant(row.statusKey),
        align: 'center',
        customTemplate: statusTemplate,
        width: '20%',
      },
    ],
    actions: [],
  };
}
