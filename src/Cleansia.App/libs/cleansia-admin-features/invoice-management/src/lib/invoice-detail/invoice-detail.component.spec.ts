/* Test doubles below intentionally mirror the real shared-component selectors
   and the PrimeNG-compatible `onClick` output so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import {
  AdminClient,
  AdminPayoutDetailsService,
  EmployeeInvoiceDetailDto,
  EmployeeInvoiceStatus,
  MaskedPayoutDetails,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import {
  DialogService as ConfirmDialogService,
  PermissionService,
  SnackbarService,
} from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { AdminPayrollOpsComponent } from './components';
import { InvoiceDetailComponent } from './invoice-detail.component';

@Component({
  selector: 'cleansia-section',
  standalone: true,
  template: '<ng-content />',
})
class SectionStub {
  title = input<string>('');
}

@Component({ selector: 'cleansia-title', standalone: true, template: '' })
class TitleStub {
  title = input<string>('');
  level = input<number>(1);
}

@Component({ selector: 'cleansia-loader', standalone: true, template: '' })
class LoaderStub {}

@Component({
  selector: 'cleansia-status-badge',
  standalone: true,
  template: '',
})
class StatusBadgeStub {
  kind = input<string>('');
  value = input<unknown>(null);
}

@Component({ selector: 'cleansia-table', standalone: true, template: '' })
class TableStub {
  data = input<unknown[]>([]);
  columns = input<unknown[]>([]);
  actions = input<unknown[]>([]);
  config = input<unknown>(null);
}

@Component({
  selector: 'cleansia-admin-payroll-ops',
  standalone: true,
  template: '',
})
class PayrollOpsStub {
  invoice = input<unknown>(null);
  changed = output<void>();
}

@Component({
  selector: 'cleansia-textarea',
  standalone: true,
  template: '',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextareaStub),
      multi: true,
    },
  ],
})
class TextareaStub {
  label = input<string>('');
  maxLength = input<number | undefined>(undefined);
  valueChanges = output<string>();
  writeValue(): void {
    /* no-op */
  }
  registerOnChange(): void {
    /* no-op */
  }
  registerOnTouched(): void {
    /* no-op */
  }
}

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string>('');
  severity = input<string>('');
  outlined = input<boolean>(false);
  loading = input<boolean>(false);
  disabled = input<boolean>(false);
  onClick = output<void>();
}

// Total 1 250,50 less 301,00 of cash the cleaner held is 949,50 to send; the server computes all three.
const APPROVED = {
  id: 'invoice-1',
  employeeId: 'employee-1',
  status: EmployeeInvoiceStatus.Approved,
  currencyCode: 'CZK',
  subTotal: 1250.5,
  bonusAmount: 0,
  deductionAmount: 0,
  totalAmount: 1250.5,
  cashSetOffAmount: 301,
  transferAmount: 949.5,
  variableSymbol: '2026000123',
  constantSymbol: '0308',
};

describe('InvoiceDetailComponent', () => {
  let fixture: ComponentFixture<InvoiceDetailComponent>;
  let markPaidMock: jest.Mock;

  beforeEach(async () => {
    markPaidMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));

    await TestBed.configureTestingModule({
      imports: [InvoiceDetailComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: AdminClient,
          useValue: {
            adminInvoiceClient: {
              details: jest
                .fn()
                .mockReturnValue(of(EmployeeInvoiceDetailDto.fromJS(APPROVED))),
              markPaid: markPaidMock,
            },
          },
        },
        {
          provide: AdminPayoutDetailsService,
          useValue: {
            getForEmployee: jest
              .fn()
              .mockReturnValue(
                of(MaskedPayoutDetails.fromJS({ maskedAccount: '**** 4321' }))
              ),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ invoiceId: 'invoice-1' }),
            },
          },
        },
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
        {
          provide: ConfirmDialogService,
          useValue: { confirmTranslated: jest.fn() },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccessTranslated: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
      ],
    })
      .overrideComponent(InvoiceDetailComponent, {
        remove: {
          imports: [
            CleansiaButtonComponent,
            CleansiaLoaderComponent,
            CleansiaSectionComponent,
            CleansiaStatusBadgeComponent,
            CleansiaTableComponent,
            CleansiaTextareaComponent,
            CleansiaTitleComponent,
            AdminPayrollOpsComponent,
          ],
        },
        add: {
          imports: [
            ButtonStub,
            LoaderStub,
            SectionStub,
            StatusBadgeStub,
            TableStub,
            TextareaStub,
            TitleStub,
            PayrollOpsStub,
          ],
        },
      })
      .compileComponents();

    TestBed.inject(TranslateService).use('cs');
    fixture = TestBed.createComponent(InvoiceDetailComponent);
    fixture.detectChanges();
  });

  const rows = (selector: string): string[][] =>
    fixture.debugElement
      .queryAll(By.css(selector))
      .map((row) =>
        Array.from((row.nativeElement as HTMLElement).children).map(
          (cell) => cell.textContent?.trim() ?? ''
        )
      );

  const button = (label: string): ButtonStub =>
    fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((b) => b.componentInstance as ButtonStub)
      .find((b) => b.label() === label) as ButtonStub;

  it('rules the cash set off off the total and ends the statement on the amount to transfer', () => {
    expect(rows('.detail-ledger__row').slice(-3)).toEqual([
      ['pages.invoice_detail.total_amount', '1 250,50 Kč'],
      ['pages.invoice_detail.cash_set_off', '- 301,00 Kč'],
      ['pages.invoice_detail.transfer_amount', '949,50 Kč'],
    ]);
  });

  it('confirms a mark paid in place, showing what to send and where, and sends the note with it', () => {
    expect(
      fixture.debugElement.query(By.css('.detail-actions__panel'))
    ).toBeNull();

    button('pages.invoice_detail.actions.mark_paid').onClick.emit();
    fixture.detectChanges();

    expect(markPaidMock).not.toHaveBeenCalled();
    expect(rows('.detail-actions__panel .detail-grid__item')).toEqual([
      ['pages.invoice_detail.transfer_amount', '949,50 Kč'],
      ['pages.invoice_detail.variable_symbol', '2026000123'],
      ['pages.invoice_detail.constant_symbol', '0308'],
      ['pages.employee_detail.payout_details.masked_account', '**** 4321'],
    ]);

    fixture.debugElement
      .query(By.directive(TextareaStub))
      .componentInstance.valueChanges.emit('FIO ref 88');
    button('pages.invoice_detail.mark_paid_confirm.submit').onClick.emit();

    expect(markPaidMock.mock.calls[0][0].toJSON()).toEqual({
      invoiceId: 'invoice-1',
      bankTransferNote: 'FIO ref 88',
      adminNotes: undefined,
    });
  });
});
