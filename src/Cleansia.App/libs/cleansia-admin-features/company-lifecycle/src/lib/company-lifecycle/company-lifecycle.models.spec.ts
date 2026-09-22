import { CompanyLifecycleDto, CompanyLifecycleState, WindDownCompanyCommand } from '@cleansia/admin-services';
import { CleansiaAdminRoute, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import {
  buildSettlementFacts,
  buildStamps,
  buildWindDownCommand,
  formatSettlementFactValue,
  getActAvailability,
  getFactStatusKey,
  getSettlementFactsTableDefinition,
  getStateSeverity,
  isWindDownRunInProgress,
  LifecycleAct,
  SETTLEMENT_FACT_DEFINITIONS,
  startOfDay,
} from './company-lifecycle.models';

const NOW = new Date('2026-09-16T10:00:00Z');
const translate = { instant: (key: string) => key, currentLang: 'en' } as unknown as TranslateService;
const factName = (id: string) => `name:${id}`;
const formatDay = (date: Date) => `day:${date.toISOString().slice(0, 10)}`;

const settled = {
  openOrders: 0,
  openOrdersOnOrAfterWindDownFrom: 0,
  activeTemplates: 0,
  activeMemberships: 0,
  creditBalances: 0,
  pendingRefunds: 0,
  ordersAwaitingPay: 0,
  ordersAwaitingReceipt: 0,
  receiptsAwaitingFiscalRegistration: 0,
  openPayPeriods: 0,
  unpaidInvoices: 0,
  uninvoicedPayRows: 0,
  openDisputes: 0,
};

function dto(overrides: Record<string, unknown> = {}): CompanyLifecycleDto {
  return CompanyLifecycleDto.fromJS({
    name: 'Cleansia CZ',
    state: CompanyLifecycleState.Operating,
    operatesDefaultMarket: false,
    ...settled,
    ...overrides,
  });
}

const act = (dto: CompanyLifecycleDto, which: LifecycleAct) => {
  const found = getActAvailability(dto, NOW, factName, formatDay).find((a) => a.act === which);
  if (!found) throw new Error(`no act ${which}`);
  return found;
};

describe('company-lifecycle models', () => {
  describe('SETTLEMENT_FACT_DEFINITIONS', () => {
    it('names every count member of the generated DTO exactly once, plus the three lifecycle instants', () => {
      const populated = dto({
        chargebackHorizonEndsOn: NOW,
        windDownLastRunOn: NOW,
        windDownRunStartedOn: NOW,
      }) as unknown as Record<string, unknown>;
      const countMembers = Object.keys(populated).filter((key) => typeof populated[key] === 'number' && key !== 'state');
      const ids = SETTLEMENT_FACT_DEFINITIONS.map((d) => d.id);

      expect(new Set(ids).size).toBe(ids.length);
      expect(ids.filter((id) => countMembers.includes(id)).sort()).toEqual(countMembers.sort());
      expect(ids.filter((id) => !countMembers.includes(id))).toEqual([
        'chargebackHorizonEndsOn',
        'windDownLastRunOn',
        'windDownRunStartedOn',
      ]);
    });

    it('marks exactly the archive preconditions of the settlement table as such', () => {
      const preconditions = SETTLEMENT_FACT_DEFINITIONS.filter((d) => d.archivePrecondition).map((d) => d.id);

      expect(preconditions).toEqual([
        'openOrders',
        'activeMemberships',
        'creditBalances',
        'pendingRefunds',
        'ordersAwaitingPay',
        'ordersAwaitingReceipt',
        'receiptsAwaitingFiscalRegistration',
        'openPayPeriods',
        'unpaidInvoices',
        'uninvoicedPayRows',
        'openDisputes',
        'chargebackHorizonEndsOn',
      ]);
    });

    it('links each count to the admin list that counts it, and the horizon to the Company settings page', () => {
      const routeOf = (id: string) => SETTLEMENT_FACT_DEFINITIONS.find((d) => d.id === id)?.route ?? null;

      expect(routeOf('openOrders')).toBe(`/${CleansiaAdminRoute.ORDER_MANAGEMENT}`);
      expect(routeOf('openOrdersOnOrAfterWindDownFrom')).toBe(`/${CleansiaAdminRoute.ORDER_MANAGEMENT}`);
      expect(routeOf('ordersAwaitingPay')).toBe(`/${CleansiaAdminRoute.ORDER_MANAGEMENT}`);
      expect(routeOf('ordersAwaitingReceipt')).toBe(`/${CleansiaAdminRoute.ORDER_MANAGEMENT}`);
      expect(routeOf('openPayPeriods')).toBe(`/${CleansiaAdminRoute.PAY_PERIODS}`);
      expect(routeOf('uninvoicedPayRows')).toBe(`/${CleansiaAdminRoute.PAY_PERIODS}`);
      expect(routeOf('unpaidInvoices')).toBe(`/${CleansiaAdminRoute.INVOICE_MANAGEMENT}`);
      expect(routeOf('openDisputes')).toBe(`/${CleansiaAdminRoute.DISPUTE_MANAGEMENT}`);
      expect(routeOf('chargebackHorizonEndsOn')).toBe(`/${CleansiaAdminRoute.COMPANY_SETTINGS}`);
      expect(routeOf('pendingRefunds')).toBeNull();
      expect(routeOf('activeMemberships')).toBeNull();
      expect(routeOf('creditBalances')).toBeNull();
    });
  });

  describe('buildSettlementFacts', () => {
    it('renders one row per definition in definition order with the DTO count on it', () => {
      const facts = buildSettlementFacts(dto({ openOrders: 3, openDisputes: 1 }), NOW);

      expect(facts.map((f) => f.id)).toEqual(SETTLEMENT_FACT_DEFINITIONS.map((d) => d.id));
      expect(facts.find((f) => f.id === 'openOrders')).toMatchObject({ kind: 'count', count: 3, status: 'blocking' });
      expect(facts.find((f) => f.id === 'openDisputes')).toMatchObject({ count: 1, status: 'blocking' });
      expect(facts.find((f) => f.id === 'pendingRefunds')).toMatchObject({ count: 0, status: 'settled' });
    });

    it('reads a non-zero informational count as informational, never as blocking', () => {
      const facts = buildSettlementFacts(dto({ openOrdersOnOrAfterWindDownFrom: 2, activeTemplates: 5 }), NOW);

      expect(facts.find((f) => f.id === 'openOrdersOnOrAfterWindDownFrom')).toMatchObject({
        count: 2,
        status: 'informational',
      });
      expect(facts.find((f) => f.id === 'activeTemplates')).toMatchObject({ count: 5, status: 'informational' });
    });

    it('reads a chargeback horizon in the future as blocking, a past or absent one as settled', () => {
      const future = new Date('2026-12-01T00:00:00Z');
      const past = new Date('2026-09-15T00:00:00Z');

      expect(buildSettlementFacts(dto({ chargebackHorizonEndsOn: future }), NOW).find((f) => f.id === 'chargebackHorizonEndsOn')).toMatchObject({
        kind: 'date',
        date: future,
        status: 'blocking',
      });
      expect(buildSettlementFacts(dto({ chargebackHorizonEndsOn: past }), NOW).find((f) => f.id === 'chargebackHorizonEndsOn')).toMatchObject({
        status: 'settled',
      });
      expect(buildSettlementFacts(dto(), NOW).find((f) => f.id === 'chargebackHorizonEndsOn')).toMatchObject({
        date: null,
        status: 'settled',
      });
    });

    it('carries the two run instants as informational dates', () => {
      const last = new Date('2026-09-10T00:00:00Z');
      const facts = buildSettlementFacts(dto({ windDownLastRunOn: last }), NOW);

      expect(facts.find((f) => f.id === 'windDownLastRunOn')).toMatchObject({ kind: 'date', date: last, status: 'informational' });
      expect(facts.find((f) => f.id === 'windDownRunStartedOn')).toMatchObject({ kind: 'date', date: null, status: 'informational' });
    });
  });

  describe('formatSettlementFactValue', () => {
    const fact = (id: string, overrides: Record<string, unknown>) => {
      const found = buildSettlementFacts(dto(overrides), NOW).find((f) => f.id === id);
      if (!found) throw new Error(`no fact ${id}`);
      return found;
    };

    it('renders a count as its number', () => {
      expect(formatSettlementFactValue(fact('openOrders', { openOrders: 7 }), translate, formatDay)).toBe('7');
    });

    it('renders the horizon as the date the archive becomes admissible, or the no-horizon sentence', () => {
      expect(
        formatSettlementFactValue(fact('chargebackHorizonEndsOn', { chargebackHorizonEndsOn: NOW }), translate, formatDay)
      ).toBe('pages.company_lifecycle.values.archive_admissible_from');
      expect(formatSettlementFactValue(fact('chargebackHorizonEndsOn', {}), translate, formatDay)).toBe(
        'pages.company_lifecycle.values.no_horizon'
      );
    });

    it('renders the run instants as a day, "never" for no run yet and "not running" for no run in progress', () => {
      expect(formatSettlementFactValue(fact('windDownLastRunOn', { windDownLastRunOn: NOW }), translate, formatDay)).toBe(
        'day:2026-09-16'
      );
      expect(formatSettlementFactValue(fact('windDownLastRunOn', {}), translate, formatDay)).toBe(
        'pages.company_lifecycle.values.never'
      );
      expect(formatSettlementFactValue(fact('windDownRunStartedOn', {}), translate, formatDay)).toBe(
        'pages.company_lifecycle.values.not_running'
      );
    });
  });

  describe('isWindDownRunInProgress', () => {
    it('is true for a run stamp younger than an hour and false for an older or absent one', () => {
      expect(isWindDownRunInProgress(dto({ windDownRunStartedOn: new Date('2026-09-16T09:30:00Z') }), NOW)).toBe(true);
      expect(isWindDownRunInProgress(dto({ windDownRunStartedOn: new Date('2026-09-16T08:59:00Z') }), NOW)).toBe(false);
      expect(isWindDownRunInProgress(dto(), NOW)).toBe(false);
    });
  });

  describe('getActAvailability — the state table', () => {
    it('lists the four acts in order, each with its policy', () => {
      const acts = getActAvailability(dto(), NOW, factName, formatDay);

      expect(acts.map((a) => a.act)).toEqual([
        LifecycleAct.Deactivate,
        LifecycleAct.Reactivate,
        LifecycleAct.WindDown,
        LifecycleAct.Archive,
      ]);
      expect(acts.map((a) => a.policy)).toEqual([
        Policy.CanDeactivateCompany,
        Policy.CanReactivateCompany,
        Policy.CanWindDownCompany,
        Policy.CanArchiveCompany,
      ]);
    });

    it('Operating, not the default market: deactivate and wind down enabled, reactivate and archive refused with their reasons', () => {
      const operating = dto();

      expect(act(operating, LifecycleAct.Deactivate)).toMatchObject({ enabled: true, reasons: [] });
      expect(act(operating, LifecycleAct.WindDown)).toMatchObject({
        enabled: true,
        reasons: [],
        labelKey: 'pages.company_lifecycle.acts.wind_down',
      });
      expect(act(operating, LifecycleAct.Reactivate)).toMatchObject({
        enabled: false,
        reasons: [{ key: 'api.company.not_deactivated' }],
      });
      expect(act(operating, LifecycleAct.Archive)).toMatchObject({
        enabled: false,
        labelKey: 'pages.company_lifecycle.acts.archive',
        reasons: [{ key: 'api.company.not_deactivated' }, { key: 'api.company.wind_down_not_requested' }],
      });
    });

    it('Operating and holding the default market: deactivate is refused with the default-market reason', () => {
      expect(act(dto({ operatesDefaultMarket: true }), LifecycleAct.Deactivate)).toMatchObject({
        enabled: false,
        reasons: [{ key: 'api.company.operates_default_market' }],
      });
    });

    it('Winding down: deactivate enabled and the wind-down button reads "run again"', () => {
      const windingDown = dto({
        state: CompanyLifecycleState.WindingDown,
        windDownFrom: new Date('2026-10-01T00:00:00Z'),
      });

      expect(act(windingDown, LifecycleAct.Deactivate)).toMatchObject({ enabled: true });
      expect(act(windingDown, LifecycleAct.WindDown)).toMatchObject({
        enabled: true,
        labelKey: 'pages.company_lifecycle.acts.run_wind_down_again',
      });
    });

    it('Deactivated with a wind-down date and unsettled facts: reactivate and run-again enabled, archive names every non-zero fact and the future horizon', () => {
      const deactivated = dto({
        state: CompanyLifecycleState.Deactivated,
        windDownFrom: new Date('2026-09-10T00:00:00Z'),
        openOrders: 2,
        pendingRefunds: 1,
        activeTemplates: 4,
        chargebackHorizonEndsOn: new Date('2027-03-09T00:00:00Z'),
      });

      expect(act(deactivated, LifecycleAct.Deactivate)).toMatchObject({
        enabled: false,
        reasons: [{ key: 'api.company.already_deactivated' }],
      });
      expect(act(deactivated, LifecycleAct.Reactivate)).toMatchObject({ enabled: true, reasons: [] });
      expect(act(deactivated, LifecycleAct.WindDown)).toMatchObject({
        enabled: true,
        labelKey: 'pages.company_lifecycle.acts.run_wind_down_again',
      });
      expect(act(deactivated, LifecycleAct.Archive)).toEqual(
        expect.objectContaining({
          enabled: false,
          reasons: [
            {
              key: 'pages.company_lifecycle.reasons.unsettled_facts',
              params: { facts: 'name:openOrders (2), name:pendingRefunds (1)' },
            },
            { key: 'pages.company_lifecycle.reasons.within_chargeback_horizon', params: { date: 'day:2027-03-09' } },
          ],
        })
      );
    });

    it('Deactivated without a wind-down date: the wind-down button asks for a date and archive waits for the notice', () => {
      const deactivated = dto({ state: CompanyLifecycleState.Deactivated });

      expect(act(deactivated, LifecycleAct.WindDown)).toMatchObject({
        enabled: true,
        labelKey: 'pages.company_lifecycle.acts.wind_down',
      });
      expect(act(deactivated, LifecycleAct.Archive)).toMatchObject({
        enabled: false,
        reasons: [{ key: 'api.company.wind_down_not_requested' }],
      });
    });

    it('Deactivated, every fact settled and the horizon past: archive enabled', () => {
      const settledCompany = dto({
        state: CompanyLifecycleState.Deactivated,
        windDownFrom: new Date('2026-09-10T00:00:00Z'),
        chargebackHorizonEndsOn: new Date('2026-09-15T00:00:00Z'),
      });

      expect(act(settledCompany, LifecycleAct.Archive)).toMatchObject({ enabled: true, reasons: [] });
    });

    it('a run in progress refuses "run again" with the in-progress reason and leaves the other acts alone', () => {
      const running = dto({
        state: CompanyLifecycleState.Deactivated,
        windDownFrom: new Date('2026-09-10T00:00:00Z'),
        windDownRunStartedOn: new Date('2026-09-16T09:50:00Z'),
      });

      expect(act(running, LifecycleAct.WindDown)).toMatchObject({
        enabled: false,
        reasons: [{ key: 'api.company.wind_down_in_progress' }],
      });
      expect(act(running, LifecycleAct.Reactivate)).toMatchObject({ enabled: true });
    });

    it('a stale run stamp older than an hour no longer refuses "run again"', () => {
      const stale = dto({
        state: CompanyLifecycleState.Deactivated,
        windDownFrom: new Date('2026-09-10T00:00:00Z'),
        windDownRunStartedOn: new Date('2026-09-16T08:00:00Z'),
      });

      expect(act(stale, LifecycleAct.WindDown)).toMatchObject({ enabled: true });
    });

    it('Frozen: only "build archive again" is enabled, everything else refused as archived', () => {
      const frozen = dto({
        state: CompanyLifecycleState.Frozen,
        windDownFrom: new Date('2026-09-10T00:00:00Z'),
        archiveRequestedOn: NOW,
      });

      expect(act(frozen, LifecycleAct.Deactivate)).toMatchObject({ enabled: false, reasons: [{ key: 'api.company.archived' }] });
      expect(act(frozen, LifecycleAct.Reactivate)).toMatchObject({ enabled: false, reasons: [{ key: 'api.company.archived' }] });
      expect(act(frozen, LifecycleAct.WindDown)).toMatchObject({ enabled: false, reasons: [{ key: 'api.company.archived' }] });
      expect(act(frozen, LifecycleAct.Archive)).toMatchObject({
        enabled: true,
        reasons: [],
        labelKey: 'pages.company_lifecycle.acts.build_archive_again',
      });
    });

    it('Archived: nothing is enabled', () => {
      const archived = dto({
        state: CompanyLifecycleState.Archived,
        archiveRequestedOn: NOW,
        archivedOn: NOW,
        archiveManifestSha256: 'a'.repeat(64),
      });

      for (const each of getActAvailability(archived, NOW, factName, formatDay)) {
        expect(each).toMatchObject({ enabled: false, reasons: [{ key: 'api.company.archived' }] });
      }
    });
  });

  describe('buildStamps', () => {
    const formatStamp = (date: Date) => `stamp:${date.toISOString()}`;

    it('is empty for an operating company', () => {
      expect(buildStamps(dto(), formatDay, formatStamp)).toEqual([]);
    });

    it('lists every stamp the DTO carries, in lifecycle order, with the actor e-mail', () => {
      const stamps = buildStamps(
        dto({
          state: CompanyLifecycleState.Archived,
          windDownFrom: new Date('2026-10-01T00:00:00Z'),
          windDownRequestedOn: new Date('2026-09-01T08:00:00Z'),
          windDownRequestedByEmail: 'ops@cleansia.cz',
          deactivatedOn: new Date('2026-10-01T06:00:00Z'),
          deactivatedByEmail: 'ops@cleansia.cz',
          archiveRequestedOn: new Date('2027-04-01T09:00:00Z'),
          archiveRequestedByEmail: 'cfo@cleansia.cz',
          archivedOn: new Date('2027-04-01T09:05:00Z'),
        }),
        formatDay,
        formatStamp
      );

      expect(stamps).toEqual([
        {
          key: 'pages.company_lifecycle.stamps.wind_down',
          params: { from: 'day:2026-10-01', requestedOn: 'stamp:2026-09-01T08:00:00.000Z', email: 'ops@cleansia.cz' },
        },
        { key: 'pages.company_lifecycle.stamps.deactivated', params: { date: 'stamp:2026-10-01T06:00:00.000Z', email: 'ops@cleansia.cz' } },
        { key: 'pages.company_lifecycle.stamps.frozen', params: { date: 'stamp:2027-04-01T09:00:00.000Z', email: 'cfo@cleansia.cz' } },
        { key: 'pages.company_lifecycle.stamps.archived', params: { date: 'stamp:2027-04-01T09:05:00.000Z' } },
      ]);
    });
  });

  describe('fact status presentation', () => {
    it('maps each status to its locale key', () => {
      expect(getFactStatusKey('blocking')).toBe('pages.company_lifecycle.status.blocking');
    });
  });

  describe('state presentation', () => {
    it('colours the five states', () => {
      expect(getStateSeverity(CompanyLifecycleState.Operating)).toBe('success');
      expect(getStateSeverity(CompanyLifecycleState.WindingDown)).toBe('warn');
      expect(getStateSeverity(CompanyLifecycleState.Deactivated)).toBe('danger');
      expect(getStateSeverity(CompanyLifecycleState.Frozen)).toBe('info');
      expect(getStateSeverity(CompanyLifecycleState.Archived)).toBe('secondary');
    });
  });

  describe('buildWindDownCommand', () => {
    it('serialises the chosen day as a date-only wire body', () => {
      const command = buildWindDownCommand(new Date(2026, 9, 1));

      expect(command).toBeInstanceOf(WindDownCompanyCommand);
      expect(command.toJSON()).toEqual({ fromDate: '2026-10-01' });
    });

    it('serialises a re-run as a body with no date', () => {
      expect(buildWindDownCommand(undefined).toJSON()).toEqual({ fromDate: undefined });
    });
  });

  describe('startOfDay', () => {
    it('drops the time of day and keeps the local calendar day', () => {
      const day = startOfDay(new Date(2026, 8, 16, 23, 59, 59));

      expect(day.getFullYear()).toBe(2026);
      expect(day.getMonth()).toBe(8);
      expect(day.getDate()).toBe(16);
      expect(day.getHours()).toBe(0);
      expect(day.getMinutes()).toBe(0);
    });
  });

  describe('getSettlementFactsTableDefinition', () => {
    it('renders the fact name through its locale key and the value and status through the given templates', () => {
      const { columns, actions } = getSettlementFactsTableDefinition(translate);
      const column = (id: string) => columns.find((c) => c.id === id);
      const row = {
        ...buildSettlementFacts(dto({ openOrders: 1 }), NOW)[0],
        display: '1',
        statusKey: getFactStatusKey('blocking'),
      };

      expect(columns.map((c) => c.id)).toEqual(['fact', 'value', 'status']);
      expect(column('fact')?.getValue?.(row)).toBe('pages.company_lifecycle.facts.openOrders');
      expect(column('value')?.getValue?.(row)).toBe('1');
      expect(column('status')?.getValue?.(row)).toBe('pages.company_lifecycle.status.blocking');
      expect(actions).toEqual([]);
    });
  });
});
