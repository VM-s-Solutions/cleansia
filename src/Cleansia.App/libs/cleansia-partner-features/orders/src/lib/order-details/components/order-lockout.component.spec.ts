/* Test doubles below intentionally mirror the real shared-component selectors and the PrimeNG-compatible
   `onClick` output so the override-imports swap is binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
} from '@cleansia/components';
import { GetOrderPhotosOrderPhotoDto, OrderItem, OrderStatus, PhotoType } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { Subject } from 'rxjs';
import { OrderLockoutComponent } from './order-lockout.component';
import { OrderLockoutFacade } from './order-lockout.facade';

const ORDER_ID = 'ord-1';
const EMPLOYEE_ID = 'emp-1';
const START = '2026-09-29T10:00:00Z';
const REPORT = 'pages.order_details.lockout.report';

@Component({ selector: 'cleansia-section', standalone: true, template: '<ng-content />' })
class SectionStub {
  title = input<string>('');
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

@Component({
  selector: 'cleansia-textarea',
  standalone: true,
  template: '',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => TextareaStub), multi: true }],
})
class TextareaStub {
  label = input<string>('');
  placeholder = input<string>('');
  rows = input<number>(3);
  maxLength = input<number | undefined>(undefined);
  type: (value: string) => void = () => undefined;
  writeValue(): void {
    /* no-op */
  }
  registerOnChange(fn: (value: string) => void): void {
    this.type = fn;
  }
  registerOnTouched(): void {
    /* no-op */
  }
}

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly lang = signal('en');
  readonly now = signal(new Date(START).getTime());
  readonly reporting = signal(false);
  readonly uploading = signal(false);
  readonly entrancePhotos = signal<GetOrderPhotosOrderPhotoDto[]>([]);
  loadEntrancePhotos = jest.fn();
  uploadEntrancePhoto = jest.fn();
  report = jest.fn();
  wakeAt = jest.fn();
}

const order = (partial: Record<string, unknown> = {}): OrderItem =>
  OrderItem.fromJS({
    id: ORDER_ID,
    orderStatus: { value: OrderStatus.Confirmed },
    assignedEmployees: [{ employeeId: EMPLOYEE_ID }],
    cleaningDateTime: START,
    ...partial,
  });

const entrancePhoto = (): GetOrderPhotosOrderPhotoDto =>
  GetOrderPhotosOrderPhotoDto.fromJS({ id: 'p-door', photoType: PhotoType.Entrance, blobUrl: 'blob:door' });

// Owner ruling 2026-09-28, decision 11: the crew reports "cannot get in" from start + 15 minutes, with an
// entrance photo and a note of the calls made.
describe('OrderLockoutComponent', () => {
  let fixture: ComponentFixture<OrderLockoutComponent>;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [OrderLockoutComponent, TranslateModule.forRoot()],
      providers: [{ provide: SnackbarService, useValue: { showErrorTranslated: jest.fn() } }],
    })
      .overrideComponent(OrderLockoutComponent, {
        remove: { imports: [CleansiaButtonComponent, CleansiaSectionComponent, CleansiaTextareaComponent] },
        add: {
          imports: [ButtonStub, SectionStub, TextareaStub],
          providers: [{ provide: OrderLockoutFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(OrderLockoutComponent);
  });

  function render(item: OrderItem, now: string): void {
    facade.now.set(new Date(now).getTime());
    fixture.componentRef.setInput('order', item);
    fixture.componentRef.setInput('employeeId', EMPLOYEE_ID);
    fixture.detectChanges();
  }

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  const reportButton = (): ButtonStub | undefined =>
    fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((debug) => debug.componentInstance as ButtonStub)
      .find((button) => button.label() === REPORT);

  const typeNote = (note: string): void => {
    (fixture.debugElement.query(By.directive(TextareaStub)).componentInstance as TextareaStub).type(note);
  };

  it('states when the report opens and offers no report before the wait is over', () => {
    render(order(), '2026-09-29T10:05:00Z');

    expect(text()).toContain('pages.order_details.lockout.not_yet');
    expect(reportButton()).toBeUndefined();
    expect(facade.wakeAt).toHaveBeenCalledWith(new Date('2026-09-29T10:15:00Z'));
  });

  it('asks for the entrance photo first and keeps the report disabled without one', () => {
    render(order(), '2026-09-29T10:20:00Z');

    expect(text()).toContain('pages.order_details.lockout.photo_needed');
    expect(reportButton()?.disabled()).toBe(true);
    expect(facade.loadEntrancePhotos).toHaveBeenCalledWith(ORDER_ID);
  });

  it('reports with the note of the calls once an entrance photo is saved, and re-reads the job after', () => {
    facade.entrancePhotos.set([entrancePhoto()]);
    render(order(), '2026-09-29T10:20:00Z');
    const reported = jest.fn();
    fixture.componentInstance.reported.subscribe(reported);

    expect(text()).not.toContain('pages.order_details.lockout.photo_needed');
    expect(reportButton()?.disabled()).toBe(false);

    typeNote('   ');
    reportButton()?.onClick.emit();
    expect(facade.report).not.toHaveBeenCalled();

    typeNote('Called 10:02 and 10:09, no answer');
    reportButton()?.onClick.emit();
    expect(facade.report).toHaveBeenCalledWith(ORDER_ID, 'Called 10:02 and 10:09, no answer', expect.any(Function));

    const onSettled: () => void = facade.report.mock.calls[0][2];
    onSettled();
    expect(reported).toHaveBeenCalledTimes(1);
  });

  it('reads back a report already made with the calls the cleaner noted', () => {
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', { pages: { order_details: { lockout: { reported_calls: 'Calls: {{calls}}' } } } });
    translate.use('en');

    render(
      order({ lockoutReportedAt: '2026-09-29T10:17:00Z', lockoutCallAttempts: 'Called 10:02 and 10:09' }),
      '2026-09-29T10:30:00Z'
    );

    expect(text()).toContain('pages.order_details.lockout.reported_body');
    expect(text()).toContain('Calls: Called 10:02 and 10:09');
    expect(reportButton()).toBeUndefined();
  });
});

describe('the job detail', () => {
  it('mounts the cannot-get-in section for the signed-in cleaner and re-reads the job after a report', () => {
    const template = readFileSync(join(__dirname, '../order-details.component.html'), 'utf8');
    const mount = template.match(/<cleansia-partner-order-lockout[\s\S]*?\/>/)?.[0] ?? '';

    expect(mount).toContain('[order]="orderDetails()!"');
    expect(mount).toContain('[employeeId]="employeeId"');
    expect(mount).toContain('(reported)="onLockoutReported()"');
  });
});
