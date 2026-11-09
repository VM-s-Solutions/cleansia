/* The section double mirrors the real shared-component selector so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
import {
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  input,
  signal,
} from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import {
  EmployeeInvoiceDetailDto,
  EmployeeInvoiceStatus,
} from '@cleansia/partner-services';
import {
  TranslateModule,
  TranslatePipe,
  TranslateService,
} from '@ngx-translate/core';
import { InvoiceDetailComponent } from './invoice-detail.component';
import { InvoiceDetailFacade } from './invoice-detail.facade';

const NBSP = String.fromCharCode(0xa0);

@Component({
  selector: 'cleansia-section',
  standalone: true,
  template: '<ng-content />',
})
class SectionStub {
  title = input<string>('');
}

describe('InvoiceDetailComponent', () => {
  let fixture: ComponentFixture<InvoiceDetailComponent>;

  function render(overrides: Record<string, unknown> = {}): HTMLElement {
    const facade = {
      invoiceDetail: signal(
        EmployeeInvoiceDetailDto.fromJS({
          id: 'inv-1',
          invoiceNumber: 'INV-1',
          status: EmployeeInvoiceStatus.Approved,
          subTotal: 1250.5,
          bonusAmount: 0,
          deductionAmount: 0,
          totalAmount: 1250.5,
          cashSetOffAmount: 0,
          transferAmount: 1250.5,
          currencyCode: 'CZK',
          generatedAt: '2026-11-15T02:10:00Z',
          ...overrides,
        })
      ),
      loading: signal(false),
      error: signal<string | null>(null),
      loadInvoiceDetail: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [InvoiceDetailComponent, TranslateModule.forRoot()],
      providers: [
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'inv-1' } } },
        },
      ],
    }).overrideComponent(InvoiceDetailComponent, {
      set: {
        imports: [TranslatePipe, SectionStub],
        schemas: [CUSTOM_ELEMENTS_SCHEMA],
        providers: [{ provide: InvoiceDetailFacade, useValue: facade }],
      },
    });
    TestBed.inject(TranslateService).use('cs');
    fixture = TestBed.createComponent(InvoiceDetailComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const statement = (page: HTMLElement): string[][] =>
    Array.from(page.querySelectorAll('.amount-breakdown .amount-row')).map(
      (row) => [
        row.querySelector('span')?.textContent?.trim() ?? '',
        row.querySelector('.amount')?.textContent?.trim() ?? '',
      ]
    );

  // The figures are the server's; the page subtracts nothing, so it can never disagree with the PDF.
  it('ends the statement with the cash set off and the bank transfer when cash was set off', () => {
    const page = render({ cashSetOffAmount: 301, transferAmount: 949.5 });

    expect(statement(page).slice(-3)).toEqual([
      ['pages.invoice_detail.total_amount', `1${NBSP}250,50${NBSP}Kč`],
      ['pages.invoices.cash_set_off', `-301,00${NBSP}Kč`],
      ['pages.invoices.transfer_amount', `949,50${NBSP}Kč`],
    ]);
  });

  it('ends the statement at the total when no cash was set off, as the PDF does', () => {
    const page = render();

    expect(statement(page).slice(-1)).toEqual([
      ['pages.invoice_detail.total_amount', `1${NBSP}250,50${NBSP}Kč`],
    ]);
    expect(page.textContent).not.toContain('pages.invoices.cash_set_off');
    expect(page.textContent).not.toContain('pages.invoices.transfer_amount');
  });

  const period = (page: HTMLElement): string | undefined =>
    page
      .querySelector('.invoice-detail-banner__meta-item')
      ?.textContent?.trim();

  it('names the pay period by its days in the language of the session, and again after a switch', () => {
    const page = render({
      payPeriodLabel: 'Nov 01 - Nov 14, 2026',
      payPeriodStartDate: '2026-11-01',
      payPeriodEndDate: '2026-11-14',
    });

    expect(period(page)).toBe('1. 11. 2026 – 14. 11. 2026');

    TestBed.inject(TranslateService).use('en');
    fixture.detectChanges();

    expect(period(page)).toBe('Nov 1, 2026 – Nov 14, 2026');
  });

  it('puts a dash where the server sent no period days', () => {
    expect(period(render())).toBe('-');
  });
});
