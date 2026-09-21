import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, CompanyInfoListItem, PagedDataOfCompanyInfoListItem } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of } from 'rxjs';
import { CompanyInfoListFacade } from './company-info-list.facade';

describe('CompanyInfoListFacade', () => {
  let facade: CompanyInfoListFacade;
  let getPagedMock: jest.Mock;
  let deleteMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };

  const company = CompanyInfoListItem.fromJS({ id: 'ci-1', name: 'Cleansia CZ' });
  const page = PagedDataOfCompanyInfoListItem.fromJS({ data: [company], total: 1 });

  beforeEach(() => {
    TestBed.resetTestingModule();
    getPagedMock = jest.fn().mockReturnValue(of(page));
    deleteMock = jest.fn().mockReturnValue(of({ companyInfoId: 'ci-1' }));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        CompanyInfoListFacade,
        { provide: AdminClient, useValue: { adminCompanyClient: { getPaged: getPagedMock, delete: deleteMock } } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs', onLangChange: EMPTY },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(CompanyInfoListFacade);
  });

  it('loads the company infos and stores the rows and total', () => {
    facade.loadCompanyInfos();

    expect(facade.companyInfos().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  describe('deleteCompanyInfo', () => {
    it('asks in red with a delete label, deletes, toasts and re-reads the list', () => {
      facade.deleteCompanyInfo(company);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_management.delete_confirm',
        'pages.company_management.delete_company',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      );
      expect(deleteMock).toHaveBeenCalledWith('ci-1');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_management.messages.delete_success');
      expect(getPagedMock).toHaveBeenCalledTimes(1);
    });

    it('does not ask for a row without id', () => {
      facade.deleteCompanyInfo(CompanyInfoListItem.fromJS({}));

      expect(confirmMock).not.toHaveBeenCalled();
      expect(deleteMock).not.toHaveBeenCalled();
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.deleteCompanyInfo(company);

      expect(deleteMock).not.toHaveBeenCalled();
      expect(getPagedMock).not.toHaveBeenCalled();
    });
  });
});
