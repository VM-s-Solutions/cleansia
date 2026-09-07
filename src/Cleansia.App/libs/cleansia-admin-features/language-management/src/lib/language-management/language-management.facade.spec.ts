import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, LanguageListItem } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { LanguageManagementFacade } from './language-management.facade';

describe('LanguageManagementFacade', () => {
  let facade: LanguageManagementFacade;
  let getOverviewMock: jest.Mock;

  beforeEach(() => {
    TestBed.resetTestingModule();
    getOverviewMock = jest.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        LanguageManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLanguageClient: {
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

    facade = TestBed.inject(LanguageManagementFacade);
  });

  it('loads the language overview', () => {
    getOverviewMock.mockReturnValue(
      of([LanguageListItem.fromJS({ id: 'l-1', code: 'cs', name: 'Čeština' })])
    );

    facade.loadLanguages();

    expect(facade.languages().length).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  // Seeded with `of(null)`, not a plausible array: the generated client answers a non-array 200
  // and a 204 with NULL. → service-form.facade.spec.ts
  it('leaves the language list an empty array when the client answers null', () => {
    getOverviewMock.mockReturnValue(of(null));

    facade.loadLanguages();

    expect(facade.languages()).toEqual([]);
    expect(facade.initialLoading()).toBe(false);
  });
});
