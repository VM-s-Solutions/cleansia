import { TestBed } from '@angular/core/testing';
import {
  AdminMembershipClient,
  DeactivateMembershipPlanResponse,
  MembershipPlanListItem,
  PagedDataOfMembershipPlanListItem,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { MembershipPlanListFacade } from './membership-plan-list.facade';
import { BILLING_INTERVAL_WIRE } from './membership-plan-list.models';

describe('MembershipPlanListFacade', () => {
  let facade: MembershipPlanListFacade;
  let membershipClient: { getPaged: jest.Mock; deactivate: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const page = PagedDataOfMembershipPlanListItem.fromJS({
    data: [
      MembershipPlanListItem.fromJS({
        id: 'plan-1',
        code: 'PLUS_MONTHLY',
        name: 'Cleansia Plus',
        billingInterval: BILLING_INTERVAL_WIRE.monthly,
        monthlyPriceCzk: 199,
        isActive: true,
      }),
    ],
    total: 1,
  });

  beforeEach(() => {
    membershipClient = { getPaged: jest.fn(), deactivate: jest.fn() };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        MembershipPlanListFacade,
        { provide: AdminMembershipClient, useValue: membershipClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(MembershipPlanListFacade);
  });

  it('loads plans and stores data + total', () => {
    membershipClient.getPaged.mockReturnValue(of(page));

    facade.loadPlans();

    expect(membershipClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.plans().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('passes the active and search filter into getPaged', () => {
    membershipClient.getPaged.mockReturnValue(of(page));

    facade.applyFilter({ active: true, search: 'plus' });

    const args = membershipClient.getPaged.mock.calls[0];
    expect(args[0]).toBe(true);
    expect(args[1]).toBe('plus');
  });

  it('resets offset to zero when a filter is applied', () => {
    membershipClient.getPaged.mockReturnValue(of(page));

    facade.onPageChange(40, 20);
    facade.applyFilter({ search: 'plus' });

    const lastArgs = membershipClient.getPaged.mock.calls.at(-1);
    expect(lastArgs?.[2]).toBe(0);
  });

  it('forwards offset and limit on page change', () => {
    membershipClient.getPaged.mockReturnValue(of(page));

    facade.onPageChange(20, 50);

    const args = membershipClient.getPaged.mock.calls[0];
    expect(args[2]).toBe(20);
    expect(args[3]).toBe(50);
  });

  it('sets the error flag and clears loading on load failure', () => {
    membershipClient.getPaged.mockReturnValue(
      throwError(() => new Error('boom'))
    );

    facade.loadPlans();

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.plans().length).toBe(0);
  });

  it('deactivates a plan, shows success and reloads the list', () => {
    membershipClient.deactivate.mockReturnValue(
      of(DeactivateMembershipPlanResponse.fromJS({ membershipPlanId: 'plan-1' }))
    );
    membershipClient.getPaged.mockReturnValue(of(page));

    facade.deactivatePlan(
      MembershipPlanListItem.fromJS({ id: 'plan-1', isActive: true })
    );

    expect(membershipClient.deactivate).toHaveBeenCalledWith('plan-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.membership_plans.messages.deactivate_success'
    );
    expect(membershipClient.getPaged).toHaveBeenCalledTimes(1);
  });

  it('does not call deactivate for a row without id', () => {
    facade.deactivatePlan(MembershipPlanListItem.fromJS({}));
    expect(membershipClient.deactivate).not.toHaveBeenCalled();
  });

  it('maps membership.plan.not_found to its translation key on deactivate failure', () => {
    membershipClient.deactivate.mockReturnValue(
      throwError(() => ({ result: { detail: 'membership.plan.not_found' } }))
    );

    facade.deactivatePlan(
      MembershipPlanListItem.fromJS({ id: 'plan-1', isActive: true })
    );

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.membership.plan.not_found'
    );
    expect(facade.deactivating()).toBe(false);
  });

  it('falls back to the generic membership error for unknown codes', () => {
    membershipClient.deactivate.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.deactivatePlan(
      MembershipPlanListItem.fromJS({ id: 'plan-1', isActive: true })
    );

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.membership.plan.action_failed'
    );
  });
});
