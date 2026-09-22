/* Test doubles below intentionally mirror the real shared-component selectors
   and the PrimeNG-compatible `onClick` output so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/no-output-on-prefix, @angular-eslint/component-selector */
import { Component, forwardRef, input, output, signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { CleansiaButtonComponent, CleansiaCheckboxComponent } from '@cleansia/components';
import { WorkContractDto } from '@cleansia/partner-services';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig } from 'primeng/dynamicdialog';
import { Subject } from 'rxjs';
import { WorkContractDialogComponent } from './work-contract-dialog.component';
import { WorkContractDialogFacade } from './work-contract-dialog.facade';
import {
  WorkContractDialogData,
  WorkContractDialogMode,
  WorkContractFactRow,
} from './work-contract-dialog.models';

function valueAccessor(forwardTo: () => Type<unknown>) {
  return { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(forwardTo), multi: true };
}

@Component({
  selector: 'cleansia-checkbox',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => CheckboxStub)],
})
class CheckboxStub {
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

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string | undefined>(undefined);
  severity = input<string>('');
  outlined = input<boolean>(false);
  loading = input<boolean>(false);
  disabled = input<boolean>(false);
  onClick = output<void>();
}

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly contract = signal<WorkContractDto | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly submitting = signal(false);
  readonly accepted = signal(false);
  readonly noticeKey = signal<string | null>(null);
  readonly language = signal('en');
  readonly mode = signal<WorkContractDialogMode>(WorkContractDialogMode.Take);
  readonly factRows = signal<WorkContractFactRow[]>([]);
  readonly acceptedLanguageName = signal<string | null>(null);
  readonly canSubmit = signal(false);
  load = jest.fn((data: WorkContractDialogData) => this.mode.set(data.mode));
  retry = jest.fn();
  connectAcceptanceControl = jest.fn((control: FormControl<boolean>) => {
    control.valueChanges.subscribe((value) => this.accepted.set(value));
  });
  submit = jest.fn();
  cancel = jest.fn();
}

const TAKE: WorkContractDialogData = { mode: WorkContractDialogMode.Take, orderId: 'ord-1' };
const ACCEPT: WorkContractDialogData = { mode: WorkContractDialogMode.Accept, orderId: 'ord-1' };
const READ: WorkContractDialogData = { mode: WorkContractDialogMode.Read, acceptanceId: 'acc-1' };

function contract(overrides: Record<string, unknown> = {}): WorkContractDto {
  return WorkContractDto.fromJS({
    legalDocumentTextId: 'text-1',
    version: '2026-09-20',
    effectiveFrom: '2026-09-20',
    language: 'en',
    title: 'Contract for work',
    contentHtml: '<h2>1. Parties</h2><p>The customer and the cleaner.</p>',
    ...overrides,
  });
}

const FACT_ROWS: WorkContractFactRow[] = [
  { labelKey: 'pages.orders.work_contract.facts.order_number', value: 'CLS-42' },
  { labelKey: 'pages.orders.work_contract.facts.price', value: 'CZK 1,250' },
];

describe('WorkContractDialogComponent', () => {
  let fixture: ComponentFixture<WorkContractDialogComponent>;
  let facade: FacadeStub;

  async function mount(data: WorkContractDialogData): Promise<void> {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [WorkContractDialogComponent, TranslateModule.forRoot()],
      providers: [{ provide: DynamicDialogConfig, useValue: { data } }],
    })
      .overrideComponent(WorkContractDialogComponent, {
        remove: { imports: [CleansiaButtonComponent, CleansiaCheckboxComponent] },
        add: {
          imports: [ButtonStub, CheckboxStub],
          providers: [{ provide: WorkContractDialogFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(WorkContractDialogComponent);
    fixture.detectChanges();
  }

  function text(): string {
    return fixture.nativeElement.textContent as string;
  }

  function primaryButton(): ButtonStub | null {
    const buttons = fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((el) => el.componentInstance as ButtonStub);
    return buttons.find((button) => button.label() !== 'global.actions.cancel' && button.label() !== 'global.actions.close') ?? null;
  }

  function checkbox(): CheckboxStub | null {
    const el = fixture.debugElement.query(By.directive(CheckboxStub));
    return el ? (el.componentInstance as CheckboxStub) : null;
  }

  it('uses OnPush change detection', async () => {
    await mount(TAKE);
    const meta = (WorkContractDialogComponent as unknown as { ɵcmp: { onPush: boolean } }).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('hands the dialog data to the facade and connects the tick', async () => {
    await mount(TAKE);

    expect(facade.load).toHaveBeenCalledWith(TAKE);
    expect(facade.connectAcceptanceControl).toHaveBeenCalledTimes(1);
  });

  it('renders the skeleton while the contract loads', async () => {
    await mount(TAKE);
    facade.loading.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(checkbox()).toBeNull();
  });

  describe('take mode', () => {
    beforeEach(async () => {
      await mount(TAKE);
      facade.contract.set(contract());
      facade.factRows.set(FACT_ROWS);
      fixture.detectChanges();
    });

    it('renders the title, the version line, the facts and the text', () => {
      expect(text()).toContain('Contract for work');
      expect(text()).toContain('pages.orders.work_contract.version_line');
      expect(text()).toContain('pages.orders.work_contract.facts.order_number');
      expect(text()).toContain('CLS-42');
      expect(text()).toContain('CZK 1,250');
      const content = fixture.nativeElement.querySelector('.work-contract-dialog__content') as HTMLElement;
      expect(content.innerHTML).toContain('<h2>1. Parties</h2>');
    });

    it('offers the tick and a disabled "accept and take" button until ticked', () => {
      expect(checkbox()?.label()).toBe('pages.orders.work_contract.accept_checkbox');
      const button = primaryButton();
      expect(button?.label()).toBe('pages.orders.work_contract.accept_and_take');
      expect(button?.disabled()).toBe(true);

      facade.canSubmit.set(true);
      fixture.detectChanges();

      expect(primaryButton()?.disabled()).toBe(false);
    });

    it('delegates the primary button to the facade', () => {
      primaryButton()?.onClick.emit();

      expect(facade.submit).toHaveBeenCalledTimes(1);
    });

    it('delegates cancel to the facade', () => {
      const cancel = fixture.debugElement
        .queryAll(By.directive(ButtonStub))
        .map((el) => el.componentInstance as ButtonStub)
        .find((button) => button.label() === 'global.actions.cancel');

      cancel?.onClick.emit();

      expect(facade.cancel).toHaveBeenCalledTimes(1);
    });

    it('shows the "read it again" notice above the re-rendered text', () => {
      facade.noticeKey.set('api.contract.text_mismatch');
      fixture.detectChanges();

      const notice = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
      expect(notice.textContent).toContain('api.contract.text_mismatch');
      expect(checkbox()).not.toBeNull();
    });

    it('shows the "cannot be taken" notice with the button disabled when there is no text', () => {
      facade.contract.set(null);
      facade.noticeKey.set('api.legal.document_not_found');
      fixture.detectChanges();

      expect(text()).toContain('api.legal.document_not_found');
      expect(primaryButton()?.disabled()).toBe(true);
    });

    it('renders the error state with a retry when the load failed', () => {
      facade.contract.set(null);
      facade.loadFailed.set(true);
      fixture.detectChanges();

      expect(text()).toContain('pages.orders.work_contract.load_failed');
      const retry = fixture.debugElement
        .queryAll(By.directive(ButtonStub))
        .map((el) => el.componentInstance as ButtonStub)
        .find((button) => button.label() === 'global.actions.retry');
      retry?.onClick.emit();
      expect(facade.retry).toHaveBeenCalledTimes(1);
    });
  });

  describe('accept mode', () => {
    it('labels the primary button as the standalone acceptance', async () => {
      await mount(ACCEPT);
      facade.contract.set(contract());
      fixture.detectChanges();

      expect(checkbox()).not.toBeNull();
      expect(primaryButton()?.label()).toBe('pages.orders.work_contract.accept_only');
    });
  });

  describe('read mode', () => {
    beforeEach(async () => {
      await mount(READ);
      facade.contract.set(
        contract({
          acceptance: {
            acceptedOn: '2026-09-21T10:00:00Z',
            documentVersion: '2026-09-20',
            acceptedLanguage: 'cs',
          },
        })
      );
      facade.acceptedLanguageName.set('Czech');
      fixture.detectChanges();
    });

    it('shows the acceptance facts and neither the tick nor the primary button', () => {
      expect(text()).toContain('pages.orders.work_contract.accepted_on');
      expect(text()).toContain('pages.orders.work_contract.accepted_in_language');
      expect(checkbox()).toBeNull();
      expect(primaryButton()).toBeNull();
    });

    it('says nothing about the language when it is the one rendered', () => {
      facade.acceptedLanguageName.set(null);
      fixture.detectChanges();

      expect(text()).not.toContain('pages.orders.work_contract.accepted_in_language');
    });

    it('offers only a close button', () => {
      const titles = fixture.debugElement
        .queryAll(By.directive(ButtonStub))
        .map((el) => (el.componentInstance as ButtonStub).label());
      expect(titles).toEqual(['global.actions.close']);
    });
  });
});
