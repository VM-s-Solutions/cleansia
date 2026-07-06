import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminMembershipClient,
  CreateMembershipPlanResponse,
  MembershipPlanDetailDto,
  UpdateMembershipPlanResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { BILLING_INTERVAL_WIRE } from '../membership-plan-list/membership-plan-list.models';
import { MembershipPlanFormFacade } from './membership-plan-form.facade';

describe('MembershipPlanFormFacade', () => {
  let facade: MembershipPlanFormFacade;
  let membershipClient: {
    details: jest.Mock;
    create: jest.Mock;
    update: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let router: { navigate: jest.Mock };

  const detail = MembershipPlanDetailDto.fromJS({
    id: 'plan-1',
    code: 'PLUS_MONTHLY',
    name: 'Cleansia Plus',
    billingInterval: BILLING_INTERVAL_WIRE.monthly,
    monthlyPriceCzk: 199,
    stripePriceId: 'price_123',
    discountPercentage: 10,
    trialPeriodDays: 14,
    freeCancellationWindowHours: 24,
    allowsExpressUpgrade: true,
    isActive: true,
  });

  const createInput = {
    code: 'plus_yearly',
    name: 'Cleansia Plus Yearly',
    billingInterval: BILLING_INTERVAL_WIRE.yearly,
    monthlyPriceCzk: 159,
    stripePriceId: 'price_456',
    discountPercentage: 15,
    freeCancellationWindowHours: 24,
    trialPeriodDays: 14,
    allowsExpressUpgrade: true,
  };

  beforeEach(() => {
    membershipClient = {
      details: jest.fn(),
      create: jest.fn(),
      update: jest.fn(),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        MembershipPlanFormFacade,
        { provide: AdminMembershipClient, useValue: membershipClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: router },
      ],
    });

    facade = TestBed.inject(MembershipPlanFormFacade);
  });

  it('loads the plan detail', () => {
    membershipClient.details.mockReturnValue(of(detail));

    facade.loadPlan('plan-1');

    expect(membershipClient.details).toHaveBeenCalledWith('plan-1');
    expect(facade.plan()?.code).toBe('PLUS_MONTHLY');
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

  it('builds a CreateMembershipPlanCommand with an uppercased code', () => {
    membershipClient.create.mockReturnValue(
      of(CreateMembershipPlanResponse.fromJS({ membershipPlanId: 'plan-2' }))
    );

    facade.create(createInput);

    expect(membershipClient.create).toHaveBeenCalledTimes(1);
    const command = membershipClient.create.mock.calls[0][0];
    expect(command.code).toBe('PLUS_YEARLY');
    expect(command.toJSON()['billingInterval']).toBe(BILLING_INTERVAL_WIRE.yearly);
    expect(command.monthlyPriceCzk).toBe(159);
    expect(command.stripePriceId).toBe('price_456');
    expect(command.discountPercentage).toBe(15);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.membership_plans.form.success.created'
    );
    expect(router.navigate).toHaveBeenCalledWith([
      '/membership-plan-management',
    ]);
    expect(facade.saving()).toBe(false);
  });

  it('maps membership.plan.code_already_exists on create failure', () => {
    membershipClient.create.mockReturnValue(
      throwError(() => ({
        result: { detail: 'membership.plan.code_already_exists' },
      }))
    );

    facade.create(createInput);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.membership.plan.code_already_exists'
    );
    expect(router.navigate).not.toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('maps membership.plan.discount_out_of_range on create failure', () => {
    membershipClient.create.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({
          detail: 'membership.plan.discount_out_of_range',
        }),
      }))
    );

    facade.create(createInput);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.membership.plan.discount_out_of_range'
    );
  });

  it('builds an UpdateMembershipPlanCommand without touching the code', () => {
    membershipClient.update.mockReturnValue(
      of(UpdateMembershipPlanResponse.fromJS({ membershipPlanId: 'plan-1' }))
    );

    facade.update('plan-1', {
      name: 'Cleansia Plus',
      monthlyPriceCzk: 249,
      stripePriceId: 'price_real',
      discountPercentage: 12,
      freeCancellationWindowHours: 48,
      trialPeriodDays: 7,
      allowsExpressUpgrade: false,
    });

    expect(membershipClient.update).toHaveBeenCalledTimes(1);
    const [id, command] = membershipClient.update.mock.calls[0];
    expect(id).toBe('plan-1');
    expect(command.membershipPlanId).toBe('plan-1');
    expect(command.monthlyPriceCzk).toBe(249);
    expect(command.stripePriceId).toBe('price_real');
    expect('code' in command.toJSON()).toBe(false);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.membership_plans.form.success.updated'
    );
    expect(router.navigate).toHaveBeenCalledWith([
      '/membership-plan-management',
    ]);
  });

  it('does not start a second save while one is in flight', () => {
    membershipClient.create.mockReturnValue(of(undefined));
    facade.saving.set(true);

    facade.create(createInput);

    expect(membershipClient.create).not.toHaveBeenCalled();
  });
});
