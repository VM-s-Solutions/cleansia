import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminCurrencyClient,
  AdminCurrencyListItem,
  AdminMembershipClient,
  CreateMembershipPlanCommand,
  CreateMembershipPlanResponse,
  MembershipPlanDetailDto,
  UpdateMembershipPlanCommand,
  UpdateMembershipPlanResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { BILLING_INTERVAL_WIRE } from '../membership-plan-list/membership-plan-list.models';
import {
  MembershipPlanCreateInput,
  MembershipPlanFormFacade,
  MembershipPlanUpdateInput,
} from './membership-plan-form.facade';

describe('MembershipPlanFormFacade', () => {
  let facade: MembershipPlanFormFacade;
  let membershipClient: {
    details: jest.Mock;
    create: jest.Mock;
    update: jest.Mock;
  };
  let getOverviewMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let router: { navigate: jest.Mock };

  const detail = MembershipPlanDetailDto.fromJS({
    id: 'plan-1',
    code: 'PLUS_MONTHLY',
    name: 'Cleansia Plus',
    billingInterval: BILLING_INTERVAL_WIRE.monthly,
    prices: {
      CZK: { price: 199, monthlyEquivalentPrice: 199, stripePriceId: 'price_czk' },
    },
    discountPercentage: 10,
    trialPeriodDays: 0,
    freeCancellationWindowHours: 24,
    allowsExpressUpgrade: true,
    expressUpgradesPerMonth: 3,
    isActive: true,
  });

  const createInput: MembershipPlanCreateInput = {
    code: 'plus_yearly',
    name: 'Cleansia Plus Yearly',
    billingInterval: BILLING_INTERVAL_WIRE.yearly,
    prices: { CZK: { price: 2030, stripePriceId: ' price_456 ' } },
    discountPercentage: 15,
    freeCancellationWindowHours: 24,
    allowsExpressUpgrade: true,
    expressUpgradesPerMonth: 2,
  };

  const updateInput: MembershipPlanUpdateInput = {
    name: 'Cleansia Plus',
    prices: { CZK: { price: 249, stripePriceId: 'price_real' } },
    discountPercentage: 12,
    freeCancellationWindowHours: 48,
    allowsExpressUpgrade: false,
    expressUpgradesPerMonth: 5,
  };

  beforeEach(() => {
    membershipClient = {
      details: jest.fn(),
      create: jest.fn(),
      update: jest.fn(),
    };
    getOverviewMock = jest.fn().mockReturnValue(
      of([
        AdminCurrencyListItem.fromJS({
          id: 'cur-czk',
          code: 'CZK',
          symbol: 'Kč',
          name: 'Czech koruna',
          isDefault: true,
          isActive: true,
        }),
        AdminCurrencyListItem.fromJS({
          id: 'cur-eur',
          code: 'EUR',
          symbol: '€',
          name: 'Euro',
          isDefault: false,
          isActive: false,
        }),
        AdminCurrencyListItem.fromJS({ id: 'cur-x', code: '', name: 'No code' }),
      ])
    );
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        MembershipPlanFormFacade,
        { provide: AdminMembershipClient, useValue: membershipClient },
        { provide: AdminCurrencyClient, useValue: { getOverview: getOverviewMock } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: router },
      ],
    });

    facade = TestBed.inject(MembershipPlanFormFacade);
  });

  describe('currencies', () => {
    it('lists every currency with its code, active and inactive alike, and drops a row without a code', () => {
      facade.loadCurrencies();

      expect(facade.currencies()).toEqual([
        { code: 'CZK', symbol: 'Kč', name: 'Czech koruna', isDefault: true, isActive: true },
        { code: 'EUR', symbol: '€', name: 'Euro', isDefault: false, isActive: false },
      ]);
    });

    it('falls back to the code for a missing symbol and name', () => {
      getOverviewMock.mockReturnValue(
        of([AdminCurrencyListItem.fromJS({ id: 'cur-pln', code: 'PLN', isActive: true })])
      );

      facade.loadCurrencies();

      expect(facade.currencies()[0]).toEqual(
        expect.objectContaining({ code: 'PLN', symbol: 'PLN', name: 'PLN' })
      );
    });

    it('leaves the list empty when the overview read fails', () => {
      getOverviewMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadCurrencies();

      expect(facade.currencies()).toEqual([]);
    });

    it('leaves the list empty when the generated client hands back null', () => {
      getOverviewMock.mockReturnValue(of(null));

      facade.loadCurrencies();

      expect(facade.currencies()).toEqual([]);
    });
  });

  it('loads the plan detail with its per-currency prices', () => {
    membershipClient.details.mockReturnValue(of(detail));

    facade.loadPlan('plan-1');

    expect(membershipClient.details).toHaveBeenCalledWith('plan-1');
    expect(facade.plan()?.code).toBe('PLUS_MONTHLY');
    expect(facade.plan()?.prices?.['CZK']?.stripePriceId).toBe('price_czk');
    expect(facade.plan()?.prices?.['EUR']).toBeUndefined();
    expect(facade.loading()).toBe(false);
  });

  it('navigates back to the list when loading the detail fails', () => {
    membershipClient.details.mockReturnValue(throwError(() => new Error('x')));

    facade.loadPlan('plan-1');

    expect(facade.plan()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith([
      '/membership-plan-management',
    ]);
  });

  describe('create', () => {
    beforeEach(() => {
      membershipClient.create.mockReturnValue(
        of(CreateMembershipPlanResponse.fromJS({ membershipPlanId: 'plan-2' }))
      );
    });

    it('builds a CreateMembershipPlanCommand with an uppercased code and the numeric interval', () => {
      facade.create(createInput);

      expect(membershipClient.create).toHaveBeenCalledTimes(1);
      const command: CreateMembershipPlanCommand = membershipClient.create.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateMembershipPlanCommand);
      expect(command.code).toBe('PLUS_YEARLY');
      expect(command.toJSON()['billingInterval']).toBe(BILLING_INTERVAL_WIRE.yearly);
      expect(command.discountPercentage).toBe(15);
      expect(command.expressUpgradesPerMonth).toBe(2);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.membership_plans.form.success.created'
      );
      expect(router.navigate).toHaveBeenCalledWith([
        '/membership-plan-management',
      ]);
      expect(facade.saving()).toBe(false);
    });

    it('serializes exactly the price blocks it was given, keyed by currency code, with the Stripe id trimmed', () => {
      facade.create(createInput);

      const command: CreateMembershipPlanCommand = membershipClient.create.mock.calls[0][0];
      expect(command.toJSON()['prices']).toEqual({
        CZK: { price: 2030, stripePriceId: 'price_456' },
      });
      expect('monthlyPriceCzk' in command.toJSON()).toBe(false);
      expect('stripePriceId' in command.toJSON()).toBe(false);
    });

    it('sends a zero trial, the only length the server accepts, without offering the field', () => {
      facade.create(createInput);

      const command: CreateMembershipPlanCommand = membershipClient.create.mock.calls[0][0];
      expect(command.toJSON()['trialPeriodDays']).toBe(0);
    });

    it('serializes an empty prices map when no block is filled — a plan may exist unpriced', () => {
      facade.create({ ...createInput, prices: {} });

      const command: CreateMembershipPlanCommand = membershipClient.create.mock.calls[0][0];
      expect(command.toJSON()['prices']).toEqual({});
    });

    it('leaves the membership.plan.code_already_exists refusal to the interceptor toast on create failure', () => {
      membershipClient.create.mockReturnValue(
        throwError(() => ({
          result: { detail: 'membership.plan.code_already_exists' },
        }))
      );

      facade.create(createInput);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
      expect(facade.saving()).toBe(false);
    });

    it('leaves the membership.plan.stripe_price_already_used refusal to the interceptor toast on create failure', () => {
      membershipClient.create.mockReturnValue(
        throwError(() => ({
          result: { detail: 'membership.plan.stripe_price_already_used' },
        }))
      );

      facade.create(createInput);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    });

    it('leaves the currency.not_found — an unknown price key — refusal to the interceptor toast on create failure', () => {
      membershipClient.create.mockReturnValue(
        throwError(() => ({ result: { detail: 'currency.not_found' } }))
      );

      facade.create(createInput);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    });

    it('leaves the membership.plan.discount_out_of_range refusal to the interceptor toast on create failure', () => {
      membershipClient.create.mockReturnValue(
        throwError(() => ({
          response: JSON.stringify({
            detail: 'membership.plan.discount_out_of_range',
          }),
        }))
      );

      facade.create(createInput);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    });

    it('does not start a second save while one is in flight', () => {
      facade.saving.set(true);

      facade.create(createInput);

      expect(membershipClient.create).not.toHaveBeenCalled();
    });
  });

  describe('update', () => {
    beforeEach(() => {
      membershipClient.update.mockReturnValue(
        of(UpdateMembershipPlanResponse.fromJS({ membershipPlanId: 'plan-1' }))
      );
    });

    it('builds an UpdateMembershipPlanCommand without touching the code', () => {
      facade.update('plan-1', updateInput);

      expect(membershipClient.update).toHaveBeenCalledTimes(1);
      const [id, command] = membershipClient.update.mock.calls[0] as [
        string,
        UpdateMembershipPlanCommand,
      ];
      expect(id).toBe('plan-1');
      expect(command).toBeInstanceOf(UpdateMembershipPlanCommand);
      expect(command.membershipPlanId).toBe('plan-1');
      expect('code' in command.toJSON()).toBe(false);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.membership_plans.form.success.updated'
      );
      expect(router.navigate).toHaveBeenCalledWith([
        '/membership-plan-management',
      ]);
    });

    it('serializes the price blocks per currency on update', () => {
      facade.update('plan-1', updateInput);

      const [, command] = membershipClient.update.mock.calls[0] as [
        string,
        UpdateMembershipPlanCommand,
      ];
      expect(command.toJSON()['prices']).toEqual({
        CZK: { price: 249, stripePriceId: 'price_real' },
      });
    });

    it('sends a zero trial on update as well', () => {
      facade.update('plan-1', updateInput);

      const [, command] = membershipClient.update.mock.calls[0];
      expect(command.toJSON()['trialPeriodDays']).toBe(0);
    });

    it('sends the express-waiver quota it was given rather than defaulting it away', () => {
      facade.update('plan-1', { ...updateInput, expressUpgradesPerMonth: 3 });

      const [, command] = membershipClient.update.mock.calls[0];
      expect(command.toJSON()['expressUpgradesPerMonth']).toBe(3);
    });

    it('leaves the membership.plan.stripe_price_already_used refusal to the interceptor toast on update failure', () => {
      membershipClient.update.mockReturnValue(
        throwError(() => ({
          result: { detail: 'membership.plan.stripe_price_already_used' },
        }))
      );

      facade.update('plan-1', updateInput);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
      expect(facade.saving()).toBe(false);
    });
  });
});
