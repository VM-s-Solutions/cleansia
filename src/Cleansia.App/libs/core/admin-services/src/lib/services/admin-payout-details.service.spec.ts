import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import { MaskedPayoutDetails } from '../client/admin-client';
import { AdminPayoutDetailsService } from './admin-payout-details.service';

describe('AdminPayoutDetailsService', () => {
  let payoutDetails: jest.Mock;

  const createService = (): AdminPayoutDetailsService => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminClient,
          useValue: { adminEmployeeClient: { payoutDetails } },
        },
      ],
    });
    return TestBed.inject(AdminPayoutDetailsService);
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
    payoutDetails = jest.fn();
  });

  it('passes the masked record straight through, keyed by employee id', () => {
    const masked = MaskedPayoutDetails.fromJS({
      employeeId: 'emp-1',
      maskedAccount: '****3003',
      revealCount: 0,
    });
    payoutDetails.mockReturnValue(of(masked));

    expect(collect(createService().getForEmployee('emp-1')).value).toBe(masked);
    expect(payoutDetails).toHaveBeenCalledWith('emp-1');
  });

  it('normalizes a cleaner with nothing on file to null instead of an error', () => {
    payoutDetails.mockReturnValue(
      throwError(() => ({
        errors: { PayoutDetailsNotFound: 'payout.not_found' },
      }))
    );

    const outcome = collect(createService().getForEmployee('emp-1'));

    expect(outcome.value).toBeNull();
    expect(outcome.error).toBeUndefined();
  });

  it('lets every other failure through, so a broken read is not read as "nothing on file"', () => {
    const failure = { errors: { EmployeeNotFound: 'employee.not_found' } };
    payoutDetails.mockReturnValue(throwError(() => failure));

    const outcome = collect(createService().getForEmployee('emp-1'));

    expect(outcome.error).toBe(failure);
    expect(outcome.value).toBeUndefined();
  });
});
