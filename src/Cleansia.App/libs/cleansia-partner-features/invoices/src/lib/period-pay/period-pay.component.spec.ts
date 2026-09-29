/* Test doubles below intentionally mirror the real shared-component selectors so the
   override-imports swap is binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output, signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { PayPeriodDto, PeriodPaySummaryDto } from '@cleansia/partner-services';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { PeriodPayComponent } from './period-pay.component';
import { PeriodPayFacade } from './period-pay.facade';

const NBSP = String.fromCharCode(0xa0);

function valueAccessor(forwardTo: () => Type<unknown>) {
  return {
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(forwardTo),
    multi: true,
  };
}

@Component({ selector: 'cleansia-section', standalone: true, template: '<ng-content />' })
class SectionStub {
  title = input<string>('');
}

@Component({ selector: 'cleansia-loader', standalone: true, template: '' })
class LoaderStub {}

@Component({ selector: 'cleansia-title', standalone: true, template: '' })
class TitleStub {
  title = input<string>('');
  level = input<number>();
}

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string>('');
  severity = input<string>('');
  outlined = input<boolean>(false);
  onClick = output<void>();
}

@Component({
  selector: 'cleansia-select',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => SelectStub)],
})
class SelectStub {
  label = input<string>('');
  options = input<ICleansiaSelectOption[]>([]);
  showClear = input<boolean>(true);
  showErrors = input<boolean>(true);
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

@Component({ selector: 'cleansia-status-badge', standalone: true, template: '' })
class StatusBadgeStub {
  kind = input<string>('');
  value = input<unknown>(null);
}

@Component({ selector: 'cleansia-table', standalone: true, template: '' })
class TableStub {
  data = input<unknown[]>([]);
  columns = input<unknown[]>([]);
  config = input<unknown>({});
  loading = input<boolean>(false);
}

const STUB_IMPORTS = [SectionStub, LoaderStub, TitleStub, ButtonStub, SelectStub, StatusBadgeStub, TableStub];

const REAL_IMPORTS = [
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
];

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly lang = signal<string>('cs');
  readonly payPeriods = signal<PayPeriodDto[]>([
    PayPeriodDto.fromJS({ id: 'period-1', periodLabel: '1.5. - 15.5.2026', status: 'Open' }),
  ]);
  readonly summary = signal<PeriodPaySummaryDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);
  readonly periodOptions = signal<ICleansiaSelectOption[]>([]);
  readonly currencyOptions = signal<ICleansiaSelectOption[]>([]);
  readonly hasMultipleCurrencies = signal<boolean>(false);
  readonly selectedPeriod = signal<PayPeriodDto | null>(null);
  connectPeriodControl = jest.fn();
  connectCurrencyControl = jest.fn();
  init = jest.fn();
  retry = jest.fn();
}

describe('PeriodPayComponent pay breakdown', () => {
  let fixture: ComponentFixture<PeriodPayComponent>;
  let facade: FacadeStub;

  const summaryWith = (totalDirtinessPay: number) =>
    PeriodPaySummaryDto.fromJS({
      payPeriodId: 'period-1',
      payPeriodLabel: '1.5. - 15.5.2026',
      totalOrders: 1,
      totalBasePay: 650,
      totalExtrasPay: 0,
      totalDirtinessPay,
      totalExpensesPay: 0,
      totalBonusPay: 0,
      totalDeductionPay: 0,
      grandTotal: 650 + totalDirtinessPay,
      currencyCode: 'CZK',
      hasInvoice: false,
      orderPays: [],
    });

  function breakdownAmount(label: string): string | undefined {
    const rows = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.amount-breakdown .amount-row')
    );
    const row = rows.find((candidate) => candidate.querySelector('span')?.textContent?.trim() === label);
    return row?.querySelector('.amount')?.textContent?.trim();
  }

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [PeriodPayComponent, TranslateModule.forRoot()],
      providers: [{ provide: Router, useValue: { navigate: jest.fn() } }],
    })
      .overrideComponent(PeriodPayComponent, {
        remove: { imports: REAL_IMPORTS },
        add: {
          imports: STUB_IMPORTS,
          providers: [{ provide: PeriodPayFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(PeriodPayComponent);
  });

  it('shows the dirtiness term of the period in the breakdown that adds up to the grand total', () => {
    facade.summary.set(summaryWith(195));
    fixture.detectChanges();

    expect(breakdownAmount('pages.period_pay.dirtiness_pay')).toBe(`195,00${NBSP}Kč`);
    expect(breakdownAmount('pages.period_pay.grand_total')).toBe(`845,00${NBSP}Kč`);
  });

  it('shows no dirtiness row for a period with no increased or heavy job', () => {
    facade.summary.set(summaryWith(0));
    fixture.detectChanges();

    expect(breakdownAmount('pages.period_pay.dirtiness_pay')).toBeUndefined();
    expect(breakdownAmount('pages.period_pay.base_pay')).toBe(`650,00${NBSP}Kč`);
  });
});
