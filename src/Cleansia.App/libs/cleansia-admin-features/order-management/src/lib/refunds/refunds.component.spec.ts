/* Test doubles below mirror the real shared-component selectors so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, Directive, forwardRef, input, output, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { AdminRefundClient, PagedDataOfRefundListItem, RefundListItem, RefundState } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { DialogService, PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { RefundsComponent } from './refunds.component';
import { RefundsFacade } from './refunds.facade';

function valueAccessor(forwardTo: () => Type<unknown>) {
  return { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(forwardTo), multi: true };
}

@Directive()
abstract class FormStub {
  label = input<string>('');
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

@Component({ selector: 'cleansia-select', standalone: true, template: '', providers: [valueAccessor(() => SelectStub)] })
class SelectStub extends FormStub {
  options = input<unknown[]>([]);
  showClear = input<boolean>(true);
  valueChanges = output<unknown>();
}

@Component({ selector: 'cleansia-calendar', standalone: true, template: '', providers: [valueAccessor(() => CalendarStub)] })
class CalendarStub extends FormStub {
  showIcon = input<boolean>(true);
  showErrors = input<boolean>(true);
  appendTo = input<string | null>(null);
  valueChanges = output<Date | null>();
}

@Component({ selector: 'cleansia-text-input', standalone: true, template: '', providers: [valueAccessor(() => TextStub)] })
class TextStub extends FormStub {
  placeholder = input<string>('');
  valueChanges = output<string>();
}

@Component({ selector: 'cleansia-checkbox', standalone: true, template: '', providers: [valueAccessor(() => CheckboxStub)] })
class CheckboxStub extends FormStub {
  valueChanges = output<boolean>();
}

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string>('');
  severity = input<string>('');
  outlined = input<boolean>(false);
  text = input<boolean>(false);
  loading = input<boolean>(false);
  disabled = input<boolean>(false);
  onClick = output<void>();
}

@Component({ selector: 'cleansia-section', standalone: true, template: '<ng-content />' })
class SectionStub {
  title = input<string>('');
}

@Component({ selector: 'cleansia-title', standalone: true, template: '' })
class TitleStub {
  title = input<string>('');
  level = input<number>();
}

@Component({ selector: 'cleansia-loader', standalone: true, template: '' })
class LoaderStub {}

@Component({ selector: 'cleansia-status-badge', standalone: true, template: '' })
class StatusBadgeStub {
  kind = input<string>('');
  value = input<unknown>(null);
}

@Component({ selector: 'cleansia-table', standalone: true, template: '' })
class TableStub {
  data = input<unknown[]>([]);
  columns = input<unknown[]>([]);
  actions = input<unknown[]>([]);
  config = input<unknown>({});
  loading = input<boolean>(false);
  clickableRows = input<unknown>(false);
  rowClick = output<unknown>();
  pageChange = output<unknown>();
  sortChange = output<unknown>();
}

function failed(hasAccount: boolean): RefundListItem {
  return RefundListItem.fromJS({
    id: 'refund-1',
    orderId: 'order-1',
    displayOrderNumber: 'CL-1001',
    customerName: 'Jana',
    hasAccount,
    amount: 400,
    currencyCode: 'CZK',
    origin: { value: 4, name: 'PartialRefund' },
    state: { value: RefundState.Failed, name: 'Failed' },
  });
}

describe('RefundsComponent repayment panel', () => {
  let fixture: ComponentFixture<RefundsComponent>;
  let facade: RefundsFacade;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [RefundsComponent, TranslateModule.forRoot()],
      providers: [
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
        {
          provide: AdminRefundClient,
          useValue: {
            getPaged: () => of(PagedDataOfRefundListItem.fromJS({ pageNumber: 1, pageSize: 20, total: 0, data: [] })),
          },
        },
        { provide: DialogService, useValue: { confirmTranslated: () => of(false) } },
        { provide: SnackbarService, useValue: { showSuccessTranslated: jest.fn() } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    });
    TestBed.overrideComponent(RefundsComponent, {
      remove: {
        imports: [
          CleansiaButtonComponent,
          CleansiaCalendarComponent,
          CleansiaCheckboxComponent,
          CleansiaLoaderComponent,
          CleansiaSectionComponent,
          CleansiaSelectComponent,
          CleansiaStatusBadgeComponent,
          CleansiaTableComponent,
          CleansiaTextInputComponent,
          CleansiaTitleComponent,
        ],
      },
      add: {
        imports: [
          ButtonStub,
          CalendarStub,
          CheckboxStub,
          LoaderStub,
          SectionStub,
          SelectStub,
          StatusBadgeStub,
          TableStub,
          TextStub,
          TitleStub,
        ],
      },
    });
    fixture = TestBed.createComponent(RefundsComponent);
    facade = fixture.debugElement.injector.get(RefundsFacade);
    fixture.detectChanges();
  });

  const button = (label: string): ButtonStub =>
    fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((el) => el.componentInstance as ButtonStub)
      .find((b) => b.label() === label)!;

  it('keeps Record transfer disabled until a reference is typed', () => {
    facade.startRepayment(failed(true));
    fixture.detectChanges();
    expect(button('pages.refunds.repayment.record_transfer').disabled()).toBe(true);

    facade.setReference('VS-1');
    fixture.detectChanges();
    expect(button('pages.refunds.repayment.record_transfer').disabled()).toBe(false);
  });

  it('keeps Record as credit disabled until the tick is set, and for a guest offers no tick at all', () => {
    facade.startRepayment(failed(true));
    fixture.detectChanges();
    expect(button('pages.refunds.repayment.record_credit').disabled()).toBe(true);
    facade.setCreditChosen(true);
    fixture.detectChanges();
    expect(button('pages.refunds.repayment.record_credit').disabled()).toBe(false);

    facade.startRepayment(failed(false));
    facade.setCreditChosen(true);
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(CheckboxStub))).toBeNull();
    expect(button('pages.refunds.repayment.record_credit').disabled()).toBe(true);
  });
});
