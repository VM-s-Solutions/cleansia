import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { provideMockStore } from '@ngrx/store/testing';
import { Confirmation, ConfirmationService } from 'primeng/api';
import { of } from 'rxjs';
import { MembershipManagementComponent } from './membership-management.component';
import { MembershipFacade } from './membership.facade';

const DAY_MS = 24 * 60 * 60 * 1000;

function membership(trialEndsAtUtc?: Date): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = true;
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
 * claim spec cannot see them. A trialing member has no running benefit, so none of the three may
 * say one keeps going. Rendered without its template; the facade is the real one.
 */
describe('MembershipManagementComponent — what cancelling or switching says', () => {
  let confirm: jest.Mock;
  let instant: jest.Mock;
  let showSuccessTranslated: jest.Mock;

  function build(response: GetMyMembershipResponse): MembershipManagementComponent {
    confirm = jest.fn();
    instant = jest.fn((key: string) => key);
    showSuccessTranslated = jest.fn();

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
              cancel: () => of(undefined),
            },
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccessTranslated, showApiError: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant, currentLang: 'en' } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });
    TestBed.overrideComponent(MembershipManagementComponent, {
      set: {
        template: '',
        providers: [MembershipFacade, { provide: ConfirmationService, useValue: { confirm } }],
      },
    });
    const component = TestBed.createComponent(MembershipManagementComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  const asked = (): Confirmation => confirm.mock.calls[0][0];

  afterEach(() => TestBed.resetTestingModule());

  it('tells a trialing member who cancels that no paid month follows, then toasts the same', () => {
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

  it('tells a trialing member who switches when the trial ends, not that benefits keep going', () => {
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
  });
});
