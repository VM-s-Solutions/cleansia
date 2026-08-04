import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient, GetMyMembershipResponse } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderMembershipFacade } from './order-membership.facade';

function buildMembership(fields: {
  hasMembership?: boolean;
  freeCancellationWindowHours?: number;
  expressUpgradesPerMonth?: number;
  expressUpgradesRemaining?: number;
  trialEndsAtUtc?: Date;
}): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = fields.hasMembership ?? true;
  response.freeCancellationWindowHours = fields.freeCancellationWindowHours;
  response.expressUpgradesPerMonth = fields.expressUpgradesPerMonth;
  response.expressUpgradesRemaining = fields.expressUpgradesRemaining;
  response.trialEndsAtUtc = fields.trialEndsAtUtc;
  return response;
}

describe('OrderMembershipFacade', () => {
  let facade: OrderMembershipFacade;
  let membershipClient: { getMine: jest.Mock };

  function build(platform: 'server' | 'browser'): void {
    membershipClient = {
      getMine: jest.fn().mockReturnValue(
        of(
          buildMembership({
            freeCancellationWindowHours: 4,
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 2,
          }),
        ),
      ),
    };

    TestBed.configureTestingModule({
      providers: [
        OrderMembershipFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { membershipClient } },
      ],
    });

    facade = TestBed.inject(OrderMembershipFacade);
  }

  describe('load', () => {
    beforeEach(() => build('browser'));

    it('starts in the say-nothing state before anything is loaded', () => {
      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.expressWaiverStatus()).toBe('none');
      expect(facade.freeCancellationWindowHours()).toBeNull();
      expect(facade.expressUpgradesRemaining()).toBe(0);
    });

    it('skips the call for an anonymous customer', () => {
      facade.load(false);

      expect(membershipClient.getMine).not.toHaveBeenCalled();
      expect(facade.expressWaiverStatus()).toBe('none');
    });

    it('reads the membership once and exposes the server counts', () => {
      facade.load(true);

      expect(membershipClient.getMine).toHaveBeenCalledTimes(1);
      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.freeCancellationWindowHours()).toBe(4);
      expect(facade.expressUpgradesRemaining()).toBe(2);
      expect(facade.expressWaiverStatus()).toBe('available');
      expect(facade.expressWaiverAvailable()).toBe(true);
      expect(facade.expressWaiverExhausted()).toBe(false);
      expect(facade.expressWaiverPendingTrial()).toBe(false);
    });

    it('reports an exhausted member so the wizard can disclose the surcharge', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 0 })),
      );

      facade.load(true);

      expect(facade.expressWaiverExhausted()).toBe(true);
      expect(facade.expressUpgradesRemaining()).toBe(0);
    });

    it('reports a trialing member as pending, never as exhausted', () => {
      membershipClient.getMine.mockReturnValue(
        of(
          buildMembership({
            expressUpgradesPerMonth: 2,
            expressUpgradesRemaining: 0,
            trialEndsAtUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
          }),
        ),
      );

      facade.load(true);

      expect(facade.expressWaiverPendingTrial()).toBe(true);
      expect(facade.expressWaiverExhausted()).toBe(false);
    });

    it('says nothing about express for a member on a plan without the perk', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ freeCancellationWindowHours: 4, expressUpgradesPerMonth: 0 })),
      );

      facade.load(true);

      expect(facade.expressWaiverStatus()).toBe('none');
      expect(facade.freeCancellationWindowHours()).toBe(4);
    });

    it('leaves a non-member with no cancellation override and no express claim', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ hasMembership: false })),
      );

      facade.load(true);

      expect(facade.freeCancellationWindowHours()).toBeNull();
      expect(facade.expressWaiverStatus()).toBe('none');
      expect(facade.loadFailed()).toBe(false);
    });

    it('degrades to the say-nothing state on a failed read', () => {
      membershipClient.getMine.mockReturnValue(throwError(() => new Error('boom')));

      facade.load(true);

      expect(facade.loadFailed()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(facade.expressWaiverStatus()).toBe('none');
      expect(facade.freeCancellationWindowHours()).toBeNull();
    });
  });

  describe('SSR', () => {
    beforeEach(() => build('server'));

    it('does not fetch during the server render', () => {
      facade.load(true);

      expect(membershipClient.getMine).not.toHaveBeenCalled();
      expect(facade.expressWaiverStatus()).toBe('none');
    });
  });
});
