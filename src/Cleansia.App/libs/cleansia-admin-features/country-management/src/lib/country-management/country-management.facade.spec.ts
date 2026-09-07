import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, CountryListItem } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CountryManagementFacade } from './country-management.facade';

describe('CountryManagementFacade', () => {
  let facade: CountryManagementFacade;
  let getOverviewMock: jest.Mock;

  beforeEach(() => {
    TestBed.resetTestingModule();
    getOverviewMock = jest.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        CountryManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCountryClient: {
              getOverview: getOverviewMock,
              delete: jest.fn().mockReturnValue(of(null)),
            },
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(CountryManagementFacade);
  });

  it('loads the country overview', () => {
    getOverviewMock.mockReturnValue(
      of([CountryListItem.fromJS({ id: 'c-1', name: 'Czechia' })])
    );

    facade.loadCountries();

    expect(facade.countries().length).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  // Seeded with `of(null)`, not a plausible array: the generated client answers a non-array 200
  // and a 204 with NULL. → service-form.facade.spec.ts
  it('leaves the country list an empty array when the client answers null', () => {
    getOverviewMock.mockReturnValue(of(null));

    facade.loadCountries();

    expect(facade.countries()).toEqual([]);
    expect(facade.initialLoading()).toBe(false);
  });
});
