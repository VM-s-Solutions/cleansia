import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  AdminClient,
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  PagedDataOfEmployeeInvoiceDto,
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

  beforeEach(async () => {
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
          useValue: { adminInvoiceClient: invoiceClient },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(InvoiceManagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('delegates the retry-pdf table action to the facade', () => {
    const retrySpy = jest
      .spyOn(component['facade'], 'retryPdf')
      .mockImplementation(() => undefined);
    const invoice = EmployeeInvoiceDto.fromJS({ id: 'invoice-1' });

    component.retryPdf(invoice);

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
