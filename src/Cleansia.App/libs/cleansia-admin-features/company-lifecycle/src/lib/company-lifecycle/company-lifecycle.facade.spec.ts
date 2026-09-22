import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ArchiveCompanyResponse,
  CompanyLifecycleDto,
  CompanyLifecycleState,
  DeactivateCompanyResponse,
  ReactivateCompanyResponse,
  WindDownCompanyCommand,
  WindDownCompanyResponse,
} from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { CompanyLifecycleFacade } from './company-lifecycle.facade';
import { LifecycleAct } from './company-lifecycle.models';

const NOW = new Date('2026-09-16T10:00:00Z');
const WIND_DOWN_FROM = new Date('2026-10-01T00:00:00Z');
const day = (date: Date) => formatDate(date, 'en');

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

function lifecycle(overrides: Record<string, unknown> = {}): CompanyLifecycleDto {
  return CompanyLifecycleDto.fromJS({
    name: 'Cleansia CZ',
    state: CompanyLifecycleState.Operating,
    operatesDefaultMarket: false,
    ...settled,
    ...overrides,
  });
}

const operating = lifecycle();
const windingDown = lifecycle({ state: CompanyLifecycleState.WindingDown, windDownFrom: WIND_DOWN_FROM });
const deactivated = lifecycle({ state: CompanyLifecycleState.Deactivated, windDownFrom: WIND_DOWN_FROM });
const deactivatedNoWindDown = lifecycle({ state: CompanyLifecycleState.Deactivated });
const frozen = lifecycle({ state: CompanyLifecycleState.Frozen, windDownFrom: WIND_DOWN_FROM, archiveRequestedOn: NOW });

describe('CompanyLifecycleFacade', () => {
  let facade: CompanyLifecycleFacade;
  let getMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let reactivateMock: jest.Mock;
  let windDownMock: jest.Mock;
  let archiveMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };
  let onLangChange: Subject<unknown>;

  beforeEach(() => {
    jest.useFakeTimers().setSystemTime(NOW);
    getMock = jest.fn().mockReturnValue(of(operating));
    deactivateMock = jest.fn().mockReturnValue(of(DeactivateCompanyResponse.fromJS({ state: CompanyLifecycleState.Deactivated })));
    reactivateMock = jest.fn().mockReturnValue(of(ReactivateCompanyResponse.fromJS({ state: CompanyLifecycleState.Operating })));
    windDownMock = jest
      .fn()
      .mockReturnValue(of(WindDownCompanyResponse.fromJS({ state: CompanyLifecycleState.WindingDown, windDownFrom: WIND_DOWN_FROM })));
    archiveMock = jest.fn().mockReturnValue(of(ArchiveCompanyResponse.fromJS({ state: CompanyLifecycleState.Frozen, archiveRequestedOn: NOW })));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };
    onLangChange = new Subject<unknown>();

    TestBed.configureTestingModule({
      providers: [
        CompanyLifecycleFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCompanyLifecycleClient: {
              get: getMock,
              deactivate: deactivateMock,
              reactivate: reactivateMock,
              windDown: windDownMock,
              archive: archiveMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en', onLangChange },
        },
      ],
    });

    facade = TestBed.inject(CompanyLifecycleFacade);
  });

  afterEach(() => jest.useRealTimers());

  describe('load', () => {
    it('reads the lifecycle and settles the loading flags', () => {
      facade.load();

      expect(getMock).toHaveBeenCalledTimes(1);
      expect(facade.lifecycle()).toBe(operating);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('settles the error state and stops loading when the read fails', () => {
      getMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.load();

      expect(facade.hasError()).toBe(true);
      expect(facade.lifecycle()).toBeNull();
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.acts()).toEqual([]);
      expect(facade.facts()).toEqual([]);
    });

    it('clears a previous error on the next successful read', () => {
      getMock.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.load();
      expect(facade.hasError()).toBe(true);

      facade.load();

      expect(facade.hasError()).toBe(false);
      expect(facade.lifecycle()).toBe(operating);
    });

    it('drops a slow earlier response once a newer reload was issued', () => {
      const slow = new Subject<CompanyLifecycleDto>();
      const fast = new Subject<CompanyLifecycleDto>();
      getMock.mockReturnValueOnce(slow).mockReturnValueOnce(fast);

      facade.load();
      facade.load();
      expect(facade.loading()).toBe(true);
      fast.next(deactivated);
      fast.complete();
      slow.next(operating);
      slow.complete();

      expect(facade.lifecycle()).toBe(deactivated);
      expect(facade.loading()).toBe(false);
    });
  });

  describe('the derived view', () => {
    it('exposes the act matrix, the fact rows with their display value, the stamps and the state of the loaded company', () => {
      getMock.mockReturnValue(
        of(
          lifecycle({
            state: CompanyLifecycleState.Deactivated,
            windDownFrom: WIND_DOWN_FROM,
            windDownRequestedOn: new Date('2026-09-01T08:00:00Z'),
            windDownRequestedByEmail: 'ops@cleansia.cz',
            deactivatedOn: new Date('2026-10-01T06:00:00Z'),
            deactivatedByEmail: 'ops@cleansia.cz',
            openOrders: 2,
            chargebackHorizonEndsOn: new Date('2027-03-09T00:00:00Z'),
          })
        )
      );

      facade.load();

      expect(facade.acts().map((a) => [a.act, a.enabled])).toEqual([
        [LifecycleAct.Deactivate, false],
        [LifecycleAct.Reactivate, true],
        [LifecycleAct.WindDown, true],
        [LifecycleAct.Archive, false],
      ]);
      expect(facade.acts()[3].reasons).toEqual([
        { key: 'pages.company_lifecycle.reasons.unsettled_facts', params: { facts: 'pages.company_lifecycle.facts.openOrders (2)' } },
        {
          key: 'pages.company_lifecycle.reasons.within_chargeback_horizon',
          params: { date: day(new Date('2027-03-09T00:00:00Z')) },
        },
      ]);
      expect(facade.facts().find((f) => f.id === 'openOrders')).toMatchObject({
        display: '2',
        status: 'blocking',
        statusKey: 'pages.company_lifecycle.status.blocking',
        route: '/order-management',
      });
      expect(facade.facts().find((f) => f.id === 'chargebackHorizonEndsOn')).toMatchObject({
        display: 'pages.company_lifecycle.values.archive_admissible_from',
        status: 'blocking',
        route: '/company-settings',
      });
      expect(facade.stamps().map((s) => s.key)).toEqual([
        'pages.company_lifecycle.stamps.wind_down',
        'pages.company_lifecycle.stamps.deactivated',
      ]);
      expect(facade.stateSeverity()).toBe('danger');
      expect(facade.runInProgress()).toBe(false);
    });

    it('reports a run in progress off a fresh run stamp', () => {
      getMock.mockReturnValue(of(lifecycle({ state: CompanyLifecycleState.Deactivated, windDownFrom: WIND_DOWN_FROM, windDownRunStartedOn: NOW })));

      facade.load();

      expect(facade.runInProgress()).toBe(true);
      expect(facade.acts()[2]).toMatchObject({ enabled: false, reasons: [{ key: 'api.company.wind_down_in_progress' }] });
    });

    it('offers today as the earliest wind-down day', () => {
      facade.load();

      expect(facade.minWindDownDate().getTime()).toBe(new Date(2026, 8, 16).getTime());
    });

    it('rebuilds the act matrix when the language changes so the fact names in the reasons follow', () => {
      const translate = TestBed.inject(TranslateService) as unknown as { instant: (key: string) => string };
      getMock.mockReturnValue(of(lifecycle({ state: CompanyLifecycleState.Deactivated, windDownFrom: WIND_DOWN_FROM, openOrders: 1 })));
      facade.load();
      expect(facade.acts()[3].reasons[0].params).toEqual({ facts: 'pages.company_lifecycle.facts.openOrders (1)' });

      translate.instant = (key: string) => `cs:${key}`;
      onLangChange.next({ lang: 'cs' });

      expect(facade.acts()[3].reasons[0].params).toEqual({ facts: 'cs:pages.company_lifecycle.facts.openOrders (1)' });
    });
  });

  describe('deactivate', () => {
    it('confirms what changes in red, deactivates, toasts and re-reads', () => {
      facade.load();

      facade.perform(LifecycleAct.Deactivate);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_lifecycle.confirm.deactivate',
        'pages.company_lifecycle.acts.deactivate',
        { name: 'Cleansia CZ' },
        { danger: true }
      );
      expect(deactivateMock).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_lifecycle.messages.deactivated');
      expect(facade.actInFlight()).toBeNull();
      expect(getMock).toHaveBeenCalledTimes(2);
    });

    it('states that the wind-down sweep runs again with the floor lifted when a date is set', () => {
      getMock.mockReturnValue(of(windingDown));
      facade.load();

      facade.perform(LifecycleAct.Deactivate);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_lifecycle.confirm.deactivate_with_wind_down',
        'pages.company_lifecycle.acts.deactivate',
        { name: 'Cleansia CZ', date: day(WIND_DOWN_FROM) },
        { danger: true }
      );
      expect(deactivateMock).toHaveBeenCalledTimes(1);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));
      facade.load();

      facade.perform(LifecycleAct.Deactivate);

      expect(deactivateMock).not.toHaveBeenCalled();
      expect(getMock).toHaveBeenCalledTimes(1);
    });

    it('neither asks nor posts when the state table refuses the act', () => {
      getMock.mockReturnValue(of(deactivated));
      facade.load();

      facade.perform(LifecycleAct.Deactivate);

      expect(confirmMock).not.toHaveBeenCalled();
      expect(deactivateMock).not.toHaveBeenCalled();
    });
  });

  describe('reactivate', () => {
    it('confirms, reactivates, toasts and re-reads', () => {
      getMock.mockReturnValue(of(deactivatedNoWindDown));
      facade.load();

      facade.perform(LifecycleAct.Reactivate);

      expect(confirmMock).toHaveBeenCalledWith('pages.company_lifecycle.confirm.reactivate', 'pages.company_lifecycle.acts.reactivate', {
        name: 'Cleansia CZ',
      });
      expect(reactivateMock).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_lifecycle.messages.reactivated');
      expect(getMock).toHaveBeenCalledTimes(2);
    });

    it('warns that what the wind-down did does not come back when a date was set', () => {
      getMock.mockReturnValue(of(deactivated));
      facade.load();

      facade.perform(LifecycleAct.Reactivate);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_lifecycle.confirm.reactivate_after_wind_down',
        'pages.company_lifecycle.acts.reactivate',
        { name: 'Cleansia CZ', date: day(WIND_DOWN_FROM) }
      );
    });

    it('is refused by the state table on an operating company', () => {
      facade.load();

      facade.perform(LifecycleAct.Reactivate);

      expect(confirmMock).not.toHaveBeenCalled();
      expect(reactivateMock).not.toHaveBeenCalled();
    });
  });

  describe('wind down', () => {
    it('opens the date dialog instead of posting when no date is set yet', () => {
      facade.load();

      facade.perform(LifecycleAct.WindDown);

      expect(facade.windDownDialogOpen()).toBe(true);
      expect(confirmMock).not.toHaveBeenCalled();
      expect(windDownMock).not.toHaveBeenCalled();
    });

    it('posts the chosen day as a date-only body from the dialog, closes it, toasts and re-reads', () => {
      facade.load();
      facade.perform(LifecycleAct.WindDown);

      facade.confirmWindDown(new Date(2026, 9, 1));

      const command: WindDownCompanyCommand = windDownMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(WindDownCompanyCommand);
      expect(command.toJSON()).toEqual({ fromDate: '2026-10-01' });
      expect(facade.windDownDialogOpen()).toBe(false);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_lifecycle.messages.wind_down_requested');
      expect(getMock).toHaveBeenCalledTimes(2);
    });

    it('closes the dialog without posting', () => {
      facade.load();
      facade.perform(LifecycleAct.WindDown);

      facade.setWindDownDialogOpen(false);

      expect(facade.windDownDialogOpen()).toBe(false);
      expect(windDownMock).not.toHaveBeenCalled();
    });

    it('confirms and posts no date when the date is already set — the re-run', () => {
      getMock.mockReturnValue(of(deactivated));
      facade.load();

      facade.perform(LifecycleAct.WindDown);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_lifecycle.confirm.run_wind_down_again',
        'pages.company_lifecycle.acts.run_wind_down_again',
        { name: 'Cleansia CZ', date: day(WIND_DOWN_FROM) }
      );
      expect(facade.windDownDialogOpen()).toBe(false);
      const command: WindDownCompanyCommand = windDownMock.mock.calls[0][0];
      expect(command.toJSON()).toEqual({ fromDate: undefined });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_lifecycle.messages.wind_down_rerun');
      expect(getMock).toHaveBeenCalledTimes(2);
    });

    it('is refused by the state table while a run is in progress', () => {
      getMock.mockReturnValue(of(lifecycle({ state: CompanyLifecycleState.Deactivated, windDownFrom: WIND_DOWN_FROM, windDownRunStartedOn: NOW })));
      facade.load();

      facade.perform(LifecycleAct.WindDown);

      expect(confirmMock).not.toHaveBeenCalled();
      expect(windDownMock).not.toHaveBeenCalled();
    });
  });

  describe('archive', () => {
    it('confirms in red with every fact that must be zero and the horizon date, freezes, toasts and re-reads', () => {
      const horizon = new Date('2026-09-15T00:00:00Z');
      getMock.mockReturnValue(of(lifecycle({ state: CompanyLifecycleState.Deactivated, windDownFrom: WIND_DOWN_FROM, chargebackHorizonEndsOn: horizon })));
      facade.load();

      facade.perform(LifecycleAct.Archive);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_lifecycle.confirm.archive',
        'pages.company_lifecycle.acts.archive',
        {
          name: 'Cleansia CZ',
          facts: [
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
          ]
            .map((id) => `pages.company_lifecycle.facts.${id}`)
            .join(', '),
          date: day(horizon),
        },
        { danger: true }
      );
      expect(archiveMock).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_lifecycle.messages.archive_requested');
      expect(getMock).toHaveBeenCalledTimes(2);
    });

    it('names the absence of a horizon when the company never took a card payment', () => {
      getMock.mockReturnValue(of(deactivated));
      facade.load();

      facade.perform(LifecycleAct.Archive);

      expect(confirmMock.mock.calls[0][2]).toMatchObject({ date: 'pages.company_lifecycle.values.no_horizon' });
    });

    it('confirms the re-run of the bundle build on a frozen company', () => {
      getMock.mockReturnValue(of(frozen));
      facade.load();

      facade.perform(LifecycleAct.Archive);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_lifecycle.confirm.archive_again',
        'pages.company_lifecycle.acts.build_archive_again',
        { name: 'Cleansia CZ', frozenOn: formatDate(NOW, 'en', 'dateTime') },
        { danger: true }
      );
      expect(archiveMock).toHaveBeenCalledTimes(1);
    });

    it('is refused by the state table while a fact is unsettled', () => {
      getMock.mockReturnValue(of(lifecycle({ state: CompanyLifecycleState.Deactivated, windDownFrom: WIND_DOWN_FROM, openOrders: 1 })));
      facade.load();

      facade.perform(LifecycleAct.Archive);

      expect(confirmMock).not.toHaveBeenCalled();
      expect(archiveMock).not.toHaveBeenCalled();
    });
  });

  describe('a refused act', () => {
    it('toasts nothing, clears the in-flight act, re-reads once and sends no second request', () => {
      deactivateMock.mockReturnValue(throwError(() => new Error('company.operates_default_market')));
      facade.load();

      facade.perform(LifecycleAct.Deactivate);

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.actInFlight()).toBeNull();
      expect(deactivateMock).toHaveBeenCalledTimes(1);
      expect(getMock).toHaveBeenCalledTimes(2);
      expect(facade.lifecycle()).toBe(operating);
    });

    it('re-reads after a refused wind-down date so the page stays consistent with the server', () => {
      windDownMock.mockReturnValue(throwError(() => new Error('company.wind_down_date_in_past')));
      facade.load();
      facade.perform(LifecycleAct.WindDown);

      facade.confirmWindDown(new Date(2026, 8, 1));

      expect(facade.windDownDialogOpen()).toBe(false);
      expect(facade.actInFlight()).toBeNull();
      expect(getMock).toHaveBeenCalledTimes(2);
    });
  });

  describe('the in-flight guard', () => {
    it('marks the act in flight and ignores every act until it completes', () => {
      const pending = new Subject<DeactivateCompanyResponse>();
      deactivateMock.mockReturnValue(pending);
      facade.load();

      facade.perform(LifecycleAct.Deactivate);
      facade.perform(LifecycleAct.Deactivate);
      facade.perform(LifecycleAct.WindDown);

      expect(facade.actInFlight()).toBe(LifecycleAct.Deactivate);
      expect(confirmMock).toHaveBeenCalledTimes(1);
      expect(deactivateMock).toHaveBeenCalledTimes(1);
      expect(facade.windDownDialogOpen()).toBe(false);

      pending.next(DeactivateCompanyResponse.fromJS({ state: CompanyLifecycleState.Deactivated }));
      pending.complete();

      expect(facade.actInFlight()).toBeNull();
      expect(getMock).toHaveBeenCalledTimes(2);
    });

    it('ignores a dialog submission while another act is in flight', () => {
      const pending = new Subject<ReactivateCompanyResponse>();
      reactivateMock.mockReturnValue(pending);
      getMock.mockReturnValue(of(deactivatedNoWindDown));
      facade.load();
      facade.perform(LifecycleAct.Reactivate);

      facade.confirmWindDown(new Date(2026, 9, 1));

      expect(windDownMock).not.toHaveBeenCalled();
    });
  });
});
