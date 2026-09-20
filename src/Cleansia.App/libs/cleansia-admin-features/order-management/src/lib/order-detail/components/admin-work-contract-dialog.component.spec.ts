/* The button stub mirrors the real shared-component selector and its PrimeNG-compatible `onClick`
   output so the override-imports swap is binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { CleansiaButtonComponent } from '@cleansia/components';
import { WorkContractDto } from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig } from 'primeng/dynamicdialog';
import { Subject } from 'rxjs';
import { AdminWorkContractDialogComponent } from './admin-work-contract-dialog.component';
import { AdminWorkContractDialogFacade } from './admin-work-contract-dialog.facade';
import {
  AdminWorkContractDialogData,
  WORK_CONTRACT_HASH_ROW,
  WorkContractRow,
} from './admin-work-contract-dialog.models';

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string | undefined>(undefined);
  severity = input<string>('');
  size = input<string>('');
  outlined = input<boolean>(false);
  onClick = output<void>();
}

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly contract = signal<WorkContractDto | null>(null);
  readonly acceptedTextHash = signal<string | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly language = signal('en');
  readonly factRows = signal<WorkContractRow[]>([]);
  readonly acceptanceRows = signal<WorkContractRow[]>([]);
  readonly renderedLanguageNotice = signal<{ language: string; accepted: string } | null>(null);
  load = jest.fn();
  retry = jest.fn();
  close = jest.fn();
}

const HASH = 'e'.repeat(64);
const DATA: AdminWorkContractDialogData = { acceptanceId: 'acc-1' };

function contract(overrides: Record<string, unknown> = {}): WorkContractDto {
  return WorkContractDto.fromJS({
    legalDocumentTextId: 'text-1',
    legalDocumentId: 'doc-1',
    version: '2026-09-20',
    effectiveFrom: '2026-09-20',
    language: 'en',
    title: 'Contract for work',
    contentHtml: '<h2>1. Parties</h2><p>The customer and the cleaner.</p>',
    acceptance: {
      acceptedOn: '2026-09-21T10:00:00Z',
      documentVersion: '2026-09-20',
      acceptedLanguage: 'cs',
      orderEmployeeId: 'seat-1',
      employeeId: 'emp-1',
    },
    ...overrides,
  });
}

const FACT_ROWS: WorkContractRow[] = [
  { labelKey: 'pages.order_detail.work_contract.dialog.facts.order_number', value: 'CLS-42' },
  { labelKey: 'pages.order_detail.work_contract.dialog.facts.price', value: 'CZK 1,250' },
];

const ACCEPTANCE_ROWS: WorkContractRow[] = [
  { labelKey: 'pages.order_detail.work_contract.dialog.acceptance.accepted_on', value: '21/09/2026, 12:00:00' },
  { labelKey: 'pages.order_detail.work_contract.dialog.acceptance.version', value: '2026-09-20' },
  { labelKey: 'pages.order_detail.work_contract.dialog.acceptance.language', value: 'Czech' },
  { labelKey: WORK_CONTRACT_HASH_ROW, value: HASH },
];

describe('AdminWorkContractDialogComponent', () => {
  let fixture: ComponentFixture<AdminWorkContractDialogComponent>;
  let facade: FacadeStub;

  async function mount(data: AdminWorkContractDialogData | undefined): Promise<void> {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [AdminWorkContractDialogComponent, TranslateModule.forRoot()],
      providers: [{ provide: DynamicDialogConfig, useValue: { data } }],
    })
      .overrideComponent(AdminWorkContractDialogComponent, {
        remove: { imports: [CleansiaButtonComponent] },
        add: {
          imports: [ButtonStub],
          providers: [{ provide: AdminWorkContractDialogFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(AdminWorkContractDialogComponent);
    fixture.detectChanges();
  }

  function text(selector: string): string {
    return (fixture.nativeElement as HTMLElement).querySelector(selector)?.textContent?.trim() ?? '';
  }

  function buttons(): ButtonStub[] {
    return fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((el) => el.componentInstance as ButtonStub);
  }

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('loads the acceptance it was opened for', async () => {
    await mount(DATA);

    expect(facade.load).toHaveBeenCalledWith('acc-1');
    expect(facade.close).not.toHaveBeenCalled();
  });

  it('closes itself when opened without an acceptance', async () => {
    await mount(undefined);

    expect(facade.load).not.toHaveBeenCalled();
    expect(facade.close).toHaveBeenCalledTimes(1);
  });

  it('shows the loader and neither facts nor text while loading', async () => {
    await mount(DATA);
    facade.loading.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('cleansia-loader')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.work-contract-dialog__facts')).toBeNull();
    expect(fixture.nativeElement.querySelector('.work-contract-dialog__content')).toBeNull();
  });

  it('renders the facts, the acceptance block with the hash, and the sanitized text', async () => {
    jest.spyOn(console, 'warn').mockImplementation(() => undefined);
    await mount(DATA);
    facade.contract.set(
      contract({ contentHtml: '<h2>1. Parties</h2><p>The customer and the cleaner.</p><script>alert(1)</script>' })
    );
    facade.factRows.set(FACT_ROWS);
    facade.acceptanceRows.set(ACCEPTANCE_ROWS);
    fixture.detectChanges();

    expect(text('.work-contract-dialog__title')).toBe('Contract for work');
    expect(text('.work-contract-dialog__version')).toBe(
      'pages.order_detail.work_contract.dialog.version_line'
    );

    const facts = fixture.nativeElement.querySelectorAll('.work-contract-dialog__facts');
    expect(facts).toHaveLength(2);
    expect(facts[0].textContent).toContain('CLS-42');
    expect(facts[1].textContent).toContain(HASH);
    expect(facts[1].textContent).toContain('Czech');
    expect(facts[1].querySelector('.work-contract-dialog__fact-missing')).toBeNull();

    const content = fixture.nativeElement.querySelector('.work-contract-dialog__content') as HTMLElement;
    expect(content.querySelector('h2')?.textContent).toBe('1. Parties');
    expect(content.querySelector('script')).toBeNull();
  });

  it('says the hash could not be read instead of dropping the row', async () => {
    await mount(DATA);
    facade.contract.set(contract());
    facade.acceptanceRows.set([
      ...ACCEPTANCE_ROWS.slice(0, 3),
      {
        labelKey: WORK_CONTRACT_HASH_ROW,
        value: '',
        valueKey: 'pages.order_detail.work_contract.dialog.acceptance.hash_unavailable',
      },
    ]);
    fixture.detectChanges();

    expect(text('.work-contract-dialog__fact-missing')).toBe(
      'pages.order_detail.work_contract.dialog.acceptance.hash_unavailable'
    );
  });

  it('names the rendered and the accepted language only when the facade says they differ', async () => {
    await mount(DATA);
    facade.contract.set(contract());
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.work-contract-dialog__notice')).toBeNull();

    facade.renderedLanguageNotice.set({ language: 'English', accepted: 'Czech' });
    fixture.detectChanges();
    expect(text('.work-contract-dialog__notice')).toBe(
      'pages.order_detail.work_contract.dialog.rendered_language_differs'
    );
  });

  it('offers a retry in the error state and hands it to the facade', async () => {
    await mount(DATA);
    facade.loadFailed.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.work-contract-dialog__error')).toBeTruthy();
    const retry = buttons().find(
      (b) => b.label() === 'pages.order_detail.work_contract.dialog.retry'
    );
    expect(retry).toBeTruthy();
    retry?.onClick.emit();

    expect(facade.retry).toHaveBeenCalledTimes(1);
  });

  it('closes through the footer button', async () => {
    await mount(DATA);
    const close = buttons().find((b) => b.label() === 'global.actions.close');

    close?.onClick.emit();

    expect(facade.close).toHaveBeenCalledTimes(1);
  });
});
