import { TestBed } from '@angular/core/testing';
import { EmployeeItem, PartnerClient } from '@cleansia/partner-services';
import { PartnerPayoutDetailsService } from '@cleansia/partner-services';
import {
  DialogService,
  FileValidationErrorService,
  SnackbarService,
} from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ProfileBankFacade } from './profile-bank.facade';
import { ProfileDocumentsFacade } from './profile-documents.facade';
import { ProfileJobRadiusFacade } from './profile-job-radius.facade';
import { ProfileFacade } from './profile.facade';

/**
 * The job radius rides the profile read the page already makes. A facade spec of the section alone
 * proves the seeding logic and not the call site — and an unseeded section renders "country-wide"
 * over whatever the cleaner actually saved.
 */
describe('ProfileFacade — job radius seeding', () => {
  let getCurrentEmployee: jest.Mock;
  let getServiced: jest.Mock;

  const createFacade = (): ProfileFacade => {
    TestBed.configureTestingModule({
      providers: [
        ProfileFacade,
        ProfileBankFacade,
        ProfileDocumentsFacade,
        ProfileJobRadiusFacade,
        {
          provide: PartnerClient,
          useValue: {
            employeeClient: { getCurrentEmployee },
            countryClient: {
              getServiced,
              getOverview: jest.fn().mockReturnValue(of([])),
            },
          },
        },
        {
          provide: PartnerPayoutDetailsService,
          useValue: { getMine: jest.fn().mockReturnValue(of(null)) },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showError: jest.fn(),
            showApiError: jest.fn(),
          },
        },
        { provide: DialogService, useValue: { confirm: jest.fn() } },
        {
          provide: FileValidationErrorService,
          useValue: { handleFileValidationErrors: jest.fn() },
        },
        { provide: Store, useValue: { dispatch: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
      ],
    });

    return TestBed.inject(ProfileFacade);
  };

  beforeEach(() => {
    getCurrentEmployee = jest
      .fn()
      .mockReturnValue(
        of(EmployeeItem.fromJS({ id: 'emp-1', jobRadiusKm: 120 }))
      );
    getServiced = jest.fn().mockReturnValue(of([]));
  });

  it('holds an empty country list, not null, when the read answers a null body', () => {
    // The generated client emits NULL — not `[]` — for a 200 with a non-array body or a 204, and
    // `catchError` cannot see it because null is not an error. Unguarded, `.map()` throws inside
    // the `tap` and the catchError below it blanks the entire profile page.
    getServiced.mockReturnValue(of(null));
    const facade = createFacade();

    facade.loadProfile();

    expect(facade.countries()).toEqual([]);
    // `countries` starts at `[]`, so the line above passes either way — what proves the guard is
    // that the read completed normally. Unguarded, the throw routes into the facade's own
    // `catchError`, which calls `markUnavailable()` and blanks the page.
    expect(facade.jobRadiusFacade.loadFailed()).toBe(false);
    expect(facade.profileLoading()).toBe(false);
  });

  it('seeds the section off the profile read, with no second round trip', () => {
    const facade = createFacade();

    facade.loadProfile();

    expect(getCurrentEmployee).toHaveBeenCalledTimes(1);
    expect(facade.jobRadiusFacade.loaded()).toBe(true);
    expect(facade.jobRadiusFacade.formGroup.getRawValue()).toEqual({
      limitEnabled: true,
      radiusKm: '120',
    });
  });

  it('puts the section into its error state when the profile read fails', () => {
    getCurrentEmployee.mockReturnValue(throwError(() => new Error('offline')));
    const facade = createFacade();

    facade.loadProfile();

    expect(facade.jobRadiusFacade.loadFailed()).toBe(true);
    expect(facade.jobRadiusFacade.loaded()).toBe(false);
  });
});
