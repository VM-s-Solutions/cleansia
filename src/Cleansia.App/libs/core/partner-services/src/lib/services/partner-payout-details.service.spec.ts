import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import { MyPayoutDetails } from '../client/partner-client';
import { PartnerPayoutDetailsService } from './partner-payout-details.service';

describe('PartnerPayoutDetailsService', () => {
  let getMyPayoutDetails: jest.Mock;

  const createService = (): PartnerPayoutDetailsService => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: PartnerClient,
          useValue: { employeeClient: { getMyPayoutDetails } },
        },
      ],
    });
    return TestBed.inject(PartnerPayoutDetailsService);
  };

  const collect = <T>(source: Observable<T>): { value?: T; error?: unknown } => {
    const outcome: { value?: T; error?: unknown } = {};
    source.subscribe({
      next: (value) => (outcome.value = value),
      error: (error: unknown) => (outcome.error = error),
    });
    return outcome;
  };

  beforeEach(() => {
    getMyPayoutDetails = jest.fn();
  });

  it('passes a saved payout destination straight through', () => {
    const details = MyPayoutDetails.fromJS({
      bankCountryId: 'country-cz',
      accountNumber: '0005885638003',
      bankCode: '5500',
    });
    getMyPayoutDetails.mockReturnValue(of(details));

    expect(collect(createService().getMine()).value).toBe(details);
  });

  it('normalizes the never-saved case to null instead of an error', () => {
    getMyPayoutDetails.mockReturnValue(
      throwError(() => ({
        errors: { PayoutDetailsNotFound: 'payout.not_found' },
      }))
    );

    const outcome = collect(createService().getMine());

    expect(outcome.value).toBeNull();
    expect(outcome.error).toBeUndefined();
  });

  it('lets every other failure through, so a broken network is not read as "no details"', () => {
    const failure = { errors: { EmployeeNotFound: 'employee.not_found' } };
    getMyPayoutDetails.mockReturnValue(throwError(() => failure));

    const outcome = collect(createService().getMine());

    expect(outcome.error).toBe(failure);
    expect(outcome.value).toBeUndefined();
  });
});
