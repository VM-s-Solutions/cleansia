import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  MembershipStatus,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { provideMockStore } from '@ngrx/store/testing';
import { Confirmation, ConfirmationService } from 'primeng/api';
import { Observable, of, throwError } from 'rxjs';
import { MembershipManagementComponent } from './membership-management.component';
import { MembershipFacade } from './membership.facade';

const DAY_MS = 24 * 60 * 60 * 1000;

function membership(
  trialEndsAtUtc?: Date,
  status = MembershipStatus.Active,
): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = true;
  response.status = status;
  response.planCode = 'PLUS_MONTHLY';
  response.currencyCode = 'CZK';
  response.trialEndsAtUtc = trialEndsAtUtc;
  return response;
}

function yearlyPlan(): GetMembershipPlansResponse {
  const plan = new GetMembershipPlansResponse();
  plan.code = 'PLUS_YEARLY';
  plan.currencyCode = 'CZK';
  plan.price = 2030;
  return plan;
}

/**
 * The cancel dialog, its success toast and the switch dialog are built in code, where the template
 * claim spec cannot see them. Nothing is charged during a trial, so none of the three may say a
 * payment is taken now. Rendered without its template; the facade is the real one.
 */
describe('MembershipManagementComponent — what cancelling or switching says', () => {
  let confirm: jest.Mock;
  let instant: jest.Mock;
  let showSuccessTranslated: jest.Mock;
  let swapPlan: jest.Mock;

  function build(
    response: GetMyMembershipResponse,
    cancel: () => Observable<void> = () => of(undefined),
  ): MembershipManagementComponent {
    confirm = jest.fn();
    instant = jest.fn((key: string) => key);
    showSuccessTranslated = jest.fn();
    swapPlan = jest.fn(() => of(undefined));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideMockStore({ selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }] }),
        {
          provide: CustomerClient,
          useValue: {
            membershipClient: {
              getMine: () => of(response),
              getPlans: () => of([yearlyPlan()]),
              cancel,
              swapPlan,
            },
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccessTranslated, showApiError: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant, currentLang: 'en' } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        // The app root's, behind the shared confirm the facade asks through.
        { provide: ConfirmationService, useValue: { confirm } },
      ],
    });
    TestBed.overrideComponent(MembershipManagementComponent, {
      set: {
        template: '',
        providers: [MembershipFacade],
      },
    });
    const component = TestBed.createComponent(MembershipManagementComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  const asked = (): Confirmation => confirm.mock.calls[0][0];

  afterEach(() => TestBed.resetTestingModule());

  it('tells a trialing member who cancels that nothing is charged, then toasts the same', () => {
    const trialEnd = new Date(Date.now() + 7 * DAY_MS);
    const component = build(membership(trialEnd));

    component.confirmCancel();

    expect(asked().message).toBe('pages.membership.cancel_dialog_message_trial');
    expect(instant).toHaveBeenCalledWith('pages.membership.cancel_dialog_message_trial', {
      date: component.formatDate(trialEnd),
    });

    asked().accept?.();

    expect(showSuccessTranslated).toHaveBeenCalledWith('pages.membership.cancel_success_trial');
  });

  it('tells a trialing member who switches that the new plan is charged when the trial ends', () => {
    const trialEnd = new Date(Date.now() + 7 * DAY_MS);
    const component = build(membership(trialEnd));

    component.switchTo('PLUS_YEARLY');

    expect(asked().message).toBe('pages.membership.switch_dialog_message_trial');
    expect(instant).toHaveBeenCalledWith('pages.membership.switch_dialog_message_trial', {
      price: component.formatPrice(2030),
      date: component.formatDate(trialEnd),
    });
    // Nothing is charged during the trial, so the button does not say "pay difference".
    expect(asked().acceptLabel).toBe('pages.membership.switch_dialog_confirm_trial');
  });

  it('tells a paid member that the paid period runs to its end', () => {
    const component = build(membership());

    component.confirmCancel();
    expect(asked().message).toBe('pages.membership.cancel_dialog_message');

    asked().accept?.();
    expect(showSuccessTranslated).toHaveBeenCalledWith('pages.membership.cancel_success');

    confirm.mockClear();
    component.switchTo('PLUS_YEARLY');
    expect(asked().message).toBe('pages.membership.switch_dialog_message');
    expect(asked().acceptLabel).toBe('pages.membership.switch_dialog_confirm');
    expect(asked().rejectLabel).toBe('common.back');
  });

  it('tells a member whose renewal failed that the cancel ends it now, then toasts the same', () => {
    const component = build(membership(undefined, MembershipStatus.PastDue));

    component.confirmCancel();
    expect(asked().message).toBe('pages.membership.cancel_dialog_message_past_due');

    asked().accept?.();
    expect(showSuccessTranslated).toHaveBeenCalledWith('pages.membership.cancel_success_past_due');
  });

  // The Plus page decides what its subscribe buttons do from its own read of the membership, and a
  // cancel that ends a failed renewal now is the only way back to Plus on the web.
  it('tells the page hosting it once a failed renewal is cancelled, and not before', () => {
    const component = build(membership(undefined, MembershipStatus.PastDue));
    const cancelled = jest.fn();
    component.cancelled.subscribe(cancelled);

    component.confirmCancel();
    expect(cancelled).not.toHaveBeenCalled();

    asked().accept?.();
    expect(cancelled).toHaveBeenCalledTimes(1);
  });

  it('tells the page nothing when the cancel is refused', () => {
    const component = build(membership(undefined, MembershipStatus.PastDue), () =>
      throwError(() => new Error('refused')),
    );
    const cancelled = jest.fn();
    component.cancelled.subscribe(cancelled);

    component.confirmCancel();
    asked().accept?.();

    expect(cancelled).not.toHaveBeenCalled();
  });

  // The shared dialog answers a No as `false`, so the facade's `confirmed` check is all that stands
  // between "Back" and a cancelled or re-priced membership.
  it('switches to the chosen plan once on yes', () => {
    const component = build(membership());

    component.switchTo('PLUS_YEARLY');
    asked().accept?.();

    expect(swapPlan).toHaveBeenCalledTimes(1);
    const command: SwapMembershipPlanCommand = swapPlan.mock.calls[0][0];
    expect(command.newPlanCode).toBe('PLUS_YEARLY');
    expect(showSuccessTranslated).toHaveBeenCalledWith('pages.membership.switch_success');
  });

  it('switches nothing on No', () => {
    const component = build(membership());

    component.switchTo('PLUS_YEARLY');
    asked().reject?.();

    expect(swapPlan).not.toHaveBeenCalled();
    expect(showSuccessTranslated).not.toHaveBeenCalled();
  });

  it('cancels nothing, toasts nothing and tells the page nothing on No', () => {
    const cancel = jest.fn(() => of(undefined));
    const component = build(membership(undefined, MembershipStatus.PastDue), cancel);
    const cancelled = jest.fn();
    component.cancelled.subscribe(cancelled);

    component.confirmCancel();
    asked().reject?.();

    expect(cancel).not.toHaveBeenCalled();
    expect(showSuccessTranslated).not.toHaveBeenCalled();
    expect(cancelled).not.toHaveBeenCalled();
  });
});
