import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Params } from '@angular/router';
import {
  AdminClient,
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  PagedDataOfEmployeeInvoiceDto,
  PagedDataOfPayPeriodDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { InvoiceManagementComponent } from './invoice-management.component';
import { getInvoiceTableActions } from './invoice-management.models';

describe('InvoiceManagementComponent', () => {
  let component: InvoiceManagementComponent;
  let fixture: ComponentFixture<InvoiceManagementComponent>;
  let invoiceClient: {
    getPaged: jest.Mock;
    download: jest.Mock;
    regeneratePdf: jest.Mock;
  };
  let queryParams: Params;

  beforeEach(async () => {
    queryParams = {};
    invoiceClient = {
      getPaged: jest
        .fn()
        .mockReturnValue(
          of(PagedDataOfEmployeeInvoiceDto.fromJS({ data: [], total: 0 }))
        ),
      download: jest.fn(),
      regeneratePdf: jest.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [InvoiceManagementComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: AdminClient,
          useValue: {
            adminInvoiceClient: invoiceClient,
            adminCurrencyClient: { getOverview: jest.fn().mockReturnValue(of([])) },
            adminPayPeriodClient: {
              getPaged: jest
                .fn()
                .mockReturnValue(of(PagedDataOfPayPeriodDto.fromJS({ data: [], total: 0 }))),
            },
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get queryParamMap() {
                return convertToParamMap(queryParams);
              },
            },
          },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(InvoiceManagementComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads every invoice when no pay period is named', () => {
    const showPayPeriod = jest.spyOn(component['facade'], 'showPayPeriod');

    fixture.detectChanges();

    expect(invoiceClient.getPaged).toHaveBeenCalledTimes(1);
    expect(showPayPeriod).not.toHaveBeenCalled();
  });

  it('opens on the pay period the link names, leaving the load to its filter', () => {
    queryParams = { payPeriodId: 'period-1' };
    const showPayPeriod = jest
      .spyOn(component['facade'], 'showPayPeriod')
      .mockImplementation(() => undefined);

    fixture.detectChanges();

    expect(showPayPeriod).toHaveBeenCalledWith('period-1');
    expect(invoiceClient.getPaged).not.toHaveBeenCalled();
  });

  it('delegates the retry-pdf table action to the facade', () => {
    fixture.detectChanges();
    const retrySpy = jest
      .spyOn(component['facade'], 'retryPdf')
      .mockImplementation(() => undefined);
    const invoice = EmployeeInvoiceDto.fromJS({ id: 'invoice-1' });

    component['table']().actions[2].onClick(invoice);

    expect(retrySpy).toHaveBeenCalledWith(invoice);
  });

  it('only offers the retry-pdf action for non-cancelled invoices without a PDF', () => {
    const actions = getInvoiceTableActions(
      {
        onViewDetails: jest.fn(),
        onDownload: jest.fn(),
        onRetryPdf: jest.fn(),
      },
      TestBed.inject(TranslateService)
    );
    const retryAction = actions[2];

    expect(
      retryAction.visible?.(
        EmployeeInvoiceDto.fromJS({
          id: 'invoice-1',
          status: EmployeeInvoiceStatus.Pending,
        })
      )
    ).toBe(true);
    expect(
      retryAction.visible?.(
        EmployeeInvoiceDto.fromJS({
          id: 'invoice-2',
          pdfBlobName: 'blob.pdf',
          status: EmployeeInvoiceStatus.Pending,
        })
      )
    ).toBe(false);
    expect(
      retryAction.visible?.(
        EmployeeInvoiceDto.fromJS({
          id: 'invoice-3',
          status: EmployeeInvoiceStatus.Cancelled,
        })
      )
    ).toBe(false);
  });
});
