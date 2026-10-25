/* Test doubles below mirror the real shared-component selectors so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
import { Component, forwardRef, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, NG_VALUE_ACCESSOR } from '@angular/forms';
import { CleansiaSectionComponent, CleansiaSelectComponent, CleansiaTextInputComponent } from '@cleansia/components';
import { PackageDetails, PaymentType } from '@cleansia/partner-services';
import { TranslateModule } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { OrderPackagesComponent } from './components/order-packages.component';
import { OrderPaymentInfoComponent } from './components/order-payment-info.component';
import { showsCustomerPrice } from './order-details.helpers';

const TEMPLATE = join(__dirname, 'order-details.component.html');

@Component({ selector: 'cleansia-section', standalone: true, template: '<ng-content />' })
class SectionStub {
  title = input<string>('');
}

class ValueAccessorStub {
  writeValue(): void {
    /* no-op */
  }
  registerOnChange(): void {
    /* no-op */
  }
  registerOnTouched(): void {
    /* no-op */
  }
  setDisabledState(): void {
    /* no-op */
  }
}

@Component({
  selector: 'cleansia-text-input',
  standalone: true,
  template: '',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => TextInputStub), multi: true }],
})
class TextInputStub extends ValueAccessorStub {
  label = input<string>('');
  floatVariant = input<string>('');
  dataType = input<string>('');
  disabled = input<boolean>(false);
}

@Component({
  selector: 'cleansia-select',
  standalone: true,
  template: '',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SelectStub), multi: true }],
})
class SelectStub extends ValueAccessorStub {
  options = input<unknown[]>([]);
  label = input<string>('');
  floatVariant = input<string>('');
  disabled = input<boolean>(false);
}

/**
 * The customer's price is what a cleaner collects at the door on a cash job. On any other job no money
 * changes hands with the cleaner, so the job sheet shows how it was paid and none of the customer's
 * price: not the total, not a package's price.
 */
describe('the job sheet shows the customer price on a cash job only', () => {
  afterEach(() => TestBed.resetTestingModule());

  it.each([
    ['a cash job', true, PaymentType.Cash],
    ['a card job', false, PaymentType.Card],
    ['a job whose payment type did not arrive', false, undefined],
  ])('%s shows the customer price: %s', (_label, shown, paymentType) => {
    expect(showsCustomerPrice(paymentType)).toBe(shown);
  });

  describe('the payment section', () => {
    let fixture: ComponentFixture<OrderPaymentInfoComponent>;

    function render(showsPrice: boolean): HTMLElement {
      TestBed.configureTestingModule({ imports: [OrderPaymentInfoComponent, TranslateModule.forRoot()] });
      TestBed.overrideComponent(OrderPaymentInfoComponent, {
        remove: { imports: [CleansiaSectionComponent, CleansiaTextInputComponent, CleansiaSelectComponent] },
        add: { imports: [SectionStub, TextInputStub, SelectStub] },
      });
      fixture = TestBed.createComponent(OrderPaymentInfoComponent);
      fixture.componentRef.setInput(
        'formGroup',
        new FormGroup({ paymentType: new FormControl('Card'), totalPrice: new FormControl('2 000 Kč'), currency: new FormControl('CZK') }),
      );
      fixture.componentRef.setInput('paymentTypeOptions', []);
      fixture.componentRef.setInput('currencyOptions', []);
      fixture.componentRef.setInput('showsPrice', showsPrice);
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('shows the total beside the payment type and the currency on a cash job', () => {
      const section = render(true);

      expect(section.querySelector('[formcontrolname="totalPrice"]')).not.toBeNull();
      expect(section.querySelector('[formcontrolname="paymentType"]')).not.toBeNull();
      expect(section.querySelector('[formcontrolname="currency"]')).not.toBeNull();
    });

    it('keeps the payment type and the currency but not the total on any other job', () => {
      const section = render(false);

      expect(section.querySelector('[formcontrolname="totalPrice"]')).toBeNull();
      expect(section.querySelector('[formcontrolname="paymentType"]')).not.toBeNull();
      expect(section.querySelector('[formcontrolname="currency"]')).not.toBeNull();
    });
  });

  describe('the packages section', () => {
    function render(showsPrice: boolean): HTMLElement {
      TestBed.configureTestingModule({ imports: [OrderPackagesComponent, TranslateModule.forRoot()] });
      TestBed.overrideComponent(OrderPackagesComponent, {
        remove: { imports: [CleansiaSectionComponent] },
        add: { imports: [SectionStub] },
      });
      const fixture = TestBed.createComponent(OrderPackagesComponent);
      fixture.componentRef.setInput('packages', [PackageDetails.fromJS({ id: 'p-1', name: 'Deep clean', price: 1500 })]);
      fixture.componentRef.setInput('currencyCode', 'CZK');
      fixture.componentRef.setInput('showsPrice', showsPrice);
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('prices each package on a cash job', () => {
      const section = render(true);

      expect(section.querySelector('h5')?.textContent).toContain('Deep clean');
      expect(section.querySelector('.package-price')?.textContent).toMatch(/1\D?500/);
    });

    it('names each package without its price on any other job', () => {
      const section = render(false);

      expect(section.querySelector('h5')?.textContent).toContain('Deep clean');
      expect(section.querySelector('.package-price')).toBeNull();
    });
  });

  it('feeds both sections from the order the sheet shows', () => {
    const template = readFileSync(TEMPLATE, 'utf8');
    const tag = (selector: string): string => {
      const start = template.indexOf(`<${selector}`);
      expect(start).toBeGreaterThanOrEqual(0);
      return template.slice(start, template.indexOf('/>', start));
    };

    expect(tag('cleansia-partner-order-payment-info')).toContain('[showsPrice]="showsCustomerPrice()"');
    expect(tag('cleansia-partner-order-packages')).toContain('[showsPrice]="showsCustomerPrice()"');
  });
});
