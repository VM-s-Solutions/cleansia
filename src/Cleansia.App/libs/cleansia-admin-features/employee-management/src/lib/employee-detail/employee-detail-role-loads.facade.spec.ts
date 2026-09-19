import { TestBed } from '@angular/core/testing';
import { AdminClient, AdminEmployeeDetail } from '@cleansia/admin-services';
import { PermissionService, Policy, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of } from 'rxjs';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

/**
 * The detail page is every role's (the cleaner list is), but two of its sections are not: the
 * identity documents are Support's and the pay configuration is the Accountant's. A read the
 * role lacks answers 403, and the shared interceptor toasts every 403 — so the facade asks
 * before it reads, and a role sees no error for a section it was never shown.
 */
describe('EmployeeDetailFacade — reads gated by the role', () => {
  let facade: EmployeeDetailFacade;
  let hasPolicy: jest.Mock;
  let loadEmployeeDocuments: jest.Mock;
  let employeeSummary: jest.Mock;

  function setup(allowed: string[]): void {
    hasPolicy = jest.fn((policy: string) => allowed.includes(policy));
    loadEmployeeDocuments = jest.fn();
    employeeSummary = jest.fn().mockReturnValue(of(null));

    TestBed.configureTestingModule({
      providers: [
        EmployeeDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmployeeClient: {
              details: jest.fn().mockReturnValue(of(AdminEmployeeDetail.fromJS({ id: 'emp-1' }))),
            },
            adminCountryClient: { getOverview: jest.fn().mockReturnValue(of([])) },
            adminPayConfigClient: { employeeSummary },
          },
        },
        { provide: PermissionService, useValue: { hasPolicy } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn() } },
        { provide: TranslateService, useValue: { instant: (k: string) => k, currentLang: 'en' } },
        { provide: DialogService, useValue: { open: jest.fn() } },
        { provide: EmployeeDocumentsFacade, useValue: { loadEmployeeDocuments, ngOnDestroy: jest.fn() } },
      ],
    });

    facade = TestBed.inject(EmployeeDetailFacade);
  }

  it('reads the documents and the pay configuration for a role that holds both', () => {
    setup([Policy.CanViewEmployeeDocumentsAdmin, Policy.CanViewPayConfigs]);

    facade.loadEmployeeDetail('emp-1');
    facade.loadEmployeePayConfigs('emp-1');

    expect(loadEmployeeDocuments).toHaveBeenCalledWith('emp-1');
    expect(employeeSummary).toHaveBeenCalledWith('emp-1');
  });

  it('skips the documents read for a role that lacks it and still loads the detail', () => {
    setup([Policy.CanViewPayConfigs]);

    facade.loadEmployeeDetail('emp-1');

    expect(facade.employee()?.id).toBe('emp-1');
    expect(loadEmployeeDocuments).not.toHaveBeenCalled();
  });

  it('skips the pay configuration read for a role that lacks it', () => {
    setup([Policy.CanViewEmployeeDocumentsAdmin]);

    facade.loadEmployeePayConfigs('emp-1');

    expect(employeeSummary).not.toHaveBeenCalled();
    expect(facade.loadingPayConfigs()).toBe(false);
  });
});
