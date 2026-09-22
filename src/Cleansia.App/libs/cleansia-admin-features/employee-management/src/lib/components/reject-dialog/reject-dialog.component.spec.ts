/* The stubs mirror the real shared-component selectors and bindings so the override-imports swap
   is binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { CleansiaButtonComponent, CleansiaTextareaComponent } from '@cleansia/components';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { RejectDialogComponent, RejectDialogData } from './reject-dialog.component';

function valueAccessor(forwardTo: () => Type<unknown>) {
  return { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(forwardTo), multi: true };
}

@Component({
  selector: 'cleansia-textarea',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextareaStub)],
})
class TextareaStub {
  label = input<string>('');
  placeholder = input<string>('');
  rows = input<number>(3);
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
  buttonType = input<string>('button');
  label = input<string>('');
  icon = input<string>('');
  severity = input<string>('');
  outlined = input<boolean>(false);
  disabled = input<boolean>(false);
  loading = input<boolean>(false);
  onClick = output<void>();
}

const REJECT_EMPLOYEE = 'Zamítnout zaměstnance';

describe('RejectDialogComponent', () => {
  let fixture: ComponentFixture<RejectDialogComponent>;
  let dialogRef: { close: jest.Mock };

  async function mount(data: RejectDialogData): Promise<void> {
    dialogRef = { close: jest.fn() };

    await TestBed.configureTestingModule({
      imports: [RejectDialogComponent, TranslateModule.forRoot()],
      providers: [
        { provide: DynamicDialogConfig, useValue: { data } },
        { provide: DynamicDialogRef, useValue: dialogRef },
      ],
    })
      .overrideComponent(RejectDialogComponent, {
        remove: { imports: [CleansiaButtonComponent, CleansiaTextareaComponent] },
        add: { imports: [ButtonStub, TextareaStub] },
      })
      .compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('cs', {
      pages: { employee_management: { reject_dialog: { reject_button: REJECT_EMPLOYEE } } },
    });
    translate.use('cs');

    fixture = TestBed.createComponent(RejectDialogComponent);
    fixture.detectChanges();
  }

  function submitButton(): ButtonStub {
    const buttons = fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((el) => el.componentInstance as ButtonStub);
    return buttons[buttons.length - 1];
  }

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  // One component serves four acts; the primary must name the one it was opened for.
  it('names the act the opener passed on its primary', async () => {
    await mount({ submitLabel: 'Zrušit fakturu' });

    expect(submitButton().label()).toBe('Zrušit fakturu');
  });

  it('falls back to the employee rejection when the opener names no act', async () => {
    await mount({});

    expect(submitButton().label()).toBe(REJECT_EMPLOYEE);
  });

  it('keeps the primary on the red outline', async () => {
    await mount({});

    expect(submitButton().severity()).toBe('danger');
    expect(submitButton().outlined()).toBe(true);
  });

  it('closes with the reason once one is given', async () => {
    await mount({});
    fixture.componentInstance.form.controls.reason.setValue('Missing stamp');

    fixture.componentInstance.onReject();

    expect(dialogRef.close).toHaveBeenCalledWith({ reason: 'Missing stamp' });
  });

  it('refuses to close without a reason', async () => {
    await mount({});

    fixture.componentInstance.onReject();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.reason.touched).toBe(true);
  });
});
