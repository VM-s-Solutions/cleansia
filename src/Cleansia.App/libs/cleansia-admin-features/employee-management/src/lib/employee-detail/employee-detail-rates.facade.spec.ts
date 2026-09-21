import { TestBed } from '@angular/core/testing';
import { AdminClient, EmployeePayConfigSummaryItemDto } from '@cleansia/admin-services';
import { DialogService as ConfirmDialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of } from 'rxjs';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

/**
 * The per-service and per-package rate lines on the pay configuration section. Each amount is
 * money in the session's language, and the "per room / per bath" wording comes from the bundle
 * rather than from the template, so a Czech admin never reads `1 + 1/room + 1/bath CZK`.
 */
describe('EmployeeDetailFacade — pay rate lines', () => {
  let facade: EmployeeDetailFacade;
  let instant: jest.Mock;

  beforeEach(() => {
    instant = jest.fn((key: string, params?: Record<string, string>) =>
      params ? `${key}:${Object.values(params).join('|')}` : key
    );

    TestBed.configureTestingModule({
      providers: [
        EmployeeDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmployeeClient: { details: jest.fn().mockReturnValue(of(null)) },
            adminPayConfigClient: { employeeSummary: jest.fn().mockReturnValue(of(null)) },
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccessTranslated: jest.fn(), showErrorTranslated: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant, currentLang: 'cs' } },
        { provide: DialogService, useValue: { open: jest.fn() } },
        { provide: ConfirmDialogService, useValue: { confirmTranslated: jest.fn(() => of(true)) } },
        {
          provide: EmployeeDocumentsFacade,
          useValue: { loadEmployeeDocuments: jest.fn(), ngOnDestroy: jest.fn() },
        },
      ],
    });

    facade = TestBed.inject(EmployeeDetailFacade);
  });

  const item = (overrides: Partial<EmployeePayConfigSummaryItemDto>) =>
    EmployeePayConfigSummaryItemDto.fromJS({
      hasConfig: true,
      basePay: 500,
      extraPerRoom: 50,
      extraPerBathroom: 30.5,
      currencyCode: 'CZK',
      ...overrides,
    });

  it('writes a service rate as three money amounts through the bundle template', () => {
    expect(facade.formatServiceRate(item({}))).toBe(
      'pages.employee_detail.rate_format:500 Kč|50 Kč|30,50 Kč'
    );
    expect(instant).toHaveBeenCalledWith('pages.employee_detail.rate_format', {
      base: '500 Kč',
      room: '50 Kč',
      bath: '30,50 Kč',
    });
  });

  it('writes a package rate as the base pay alone', () => {
    expect(facade.formatPackageRate(item({ basePay: 1250 }))).toBe('1 250 Kč');
  });

  it('prints a bare number when the row carries no currency', () => {
    expect(facade.formatPackageRate(item({ basePay: 80, currencyCode: undefined }))).toBe('80');
  });
});
