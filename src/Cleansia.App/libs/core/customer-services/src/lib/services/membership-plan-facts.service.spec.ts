import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { GetMembershipPlansResponse } from '../client/customer-client';
import { MembershipPlanFactsService } from './membership-plan-facts.service';

function plan(code: string, currencyCode: string): GetMembershipPlansResponse {
  const response = new GetMembershipPlansResponse();
  response.code = code;
  response.billingInterval = 1;
  response.currencyCode = currencyCode;
  return response;
}

describe('MembershipPlanFactsService', () => {
  let getPlans: jest.Mock;
  let service: MembershipPlanFactsService;

  beforeEach(() => {
    getPlans = jest.fn().mockReturnValue(of([plan('PLUS_MONTHLY', 'CZK')]));
    TestBed.configureTestingModule({
      providers: [{ provide: CustomerClient, useValue: { membershipClient: { getPlans } } }],
    });
    service = TestBed.inject(MembershipPlanFactsService);
  });

  it('asks for the plans priced in the chosen market and labels them with the response code', () => {
    service.load('svk-id');

    expect(getPlans).toHaveBeenCalledWith('svk-id');
    expect(service.currencyCode()).toBe('CZK');
    expect(service.hasPlans()).toBe(true);
    expect(service.unavailable()).toBe(false);
  });

  it('sends no country when no market resolved', () => {
    service.load(null);

    expect(getPlans).toHaveBeenCalledWith(undefined);
  });

  it('reads once per market and again when the market changes', () => {
    service.load('cze-id');
    service.load('cze-id');
    service.load('svk-id');

    expect(getPlans).toHaveBeenCalledTimes(2);
    expect(getPlans).toHaveBeenLastCalledWith('svk-id');
  });

  // An empty list is an answer — Plus is not on sale here — and is cached like any other.
  it('reports Plus unavailable when the market lists no plan', () => {
    getPlans.mockReturnValue(of([]));

    service.load('svk-id');
    service.load('svk-id');

    expect(service.unavailable()).toBe(true);
    expect(service.hasPlans()).toBe(false);
    expect(service.currencyCode()).toBeNull();
    expect(getPlans).toHaveBeenCalledTimes(1);
  });

  it('does not claim Plus is unavailable when the read failed, and retries the next call', () => {
    getPlans.mockReturnValueOnce(throwError(() => new Error('offline')));

    service.load('cze-id');

    expect(service.unavailable()).toBe(false);
    expect(service.hasPlans()).toBe(false);
    expect(service.loading()).toBe(false);

    service.load('cze-id');
    expect(getPlans).toHaveBeenCalledTimes(2);
    expect(service.hasPlans()).toBe(true);
  });

  it('treats a null body as a failed read', () => {
    getPlans.mockReturnValue(of(null));

    service.load('cze-id');

    expect(service.plans()).toEqual([]);
    expect(service.unavailable()).toBe(false);
  });

  it('drops a slow answer for a market no longer chosen', () => {
    const slow = new Subject<GetMembershipPlansResponse[]>();
    getPlans.mockReturnValueOnce(slow).mockReturnValueOnce(of([plan('PLUS_MONTHLY', 'EUR')]));

    service.load('cze-id');
    service.load('svk-id');
    slow.next([plan('PLUS_MONTHLY', 'CZK')]);

    expect(service.currencyCode()).toBe('EUR');
  });
});
