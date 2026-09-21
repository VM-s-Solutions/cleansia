import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, LanguageListItem } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of } from 'rxjs';
import { LanguageManagementFacade } from './language-management.facade';

describe('LanguageManagementFacade', () => {
  let facade: LanguageManagementFacade;
  let getOverviewMock: jest.Mock;
  let deleteMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getOverviewMock = jest.fn().mockReturnValue(of([]));
    deleteMock = jest.fn().mockReturnValue(of(null));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        LanguageManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLanguageClient: {
              getOverview: getOverviewMock,
              delete: deleteMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => k,
            currentLang: 'cs',
            onLangChange: EMPTY,
          },
        },
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

  it('narrows the overview to the languages whose code or name contains the search, once it settles', () => {
    jest.useFakeTimers();
    getOverviewMock.mockReturnValue(
      of([
        LanguageListItem.fromJS({ id: 'l-1', code: 'cs', name: 'Čeština' }),
        LanguageListItem.fromJS({ id: 'l-2', code: 'uk', name: 'Українська' }),
      ])
    );
    facade.loadLanguages();

    facade.filterForm.patchValue({ searchTerm: 'ČEŠ' });
    jest.advanceTimersByTime(500);
    expect(facade.languages().map((l) => l.code)).toEqual(['cs']);
    expect(facade.filters.chips()).toEqual([
      { key: 'searchTerm', label: 'pages.language_management.filters.search', value: 'ČEŠ' },
    ]);

    facade.filters.reset();
    expect(facade.languages().length).toBe(2);
    jest.useRealTimers();
  });

  describe('deleteLanguage', () => {
    const language = LanguageListItem.fromJS({ id: 'l-2', code: 'uk' });

    it('asks in red with a delete label, deletes, shows success and re-reads the list', () => {
      deleteMock.mockReturnValue(of({ id: 'l-2' }));

      facade.deleteLanguage(language);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.language_management.delete_confirm',
        'pages.language_management.delete_language',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      );
      expect(deleteMock).toHaveBeenCalledWith('l-2');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.language_management.messages.delete_success');
      expect(getOverviewMock).toHaveBeenCalledTimes(1);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.deleteLanguage(language);

      expect(deleteMock).not.toHaveBeenCalled();
      expect(getOverviewMock).not.toHaveBeenCalled();
    });
  });
});
