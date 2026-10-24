/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, DebugElement, forwardRef, input, output, signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, NG_VALUE_ACCESSOR, Validators } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import {
  AdminEmployeeDetail,
  CountryFieldLabelsService,
  EmployeeEntityType,
  EmployeePayConfigSummaryDto,
  IAdminEmployeeDetail,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { PermissionService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { EmployeeDetailComponent } from './employee-detail.component';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';
import { EmployeeDocumentsSectionComponent } from './employee-documents-section.component';
import { EmployeePayoutSectionComponent } from './employee-payout-section.component';

function valueAccessor(forwardTo: () => Type<unknown>) {
  return {
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(forwardTo),
    multi: true,
  };
}

class ControlStub {
  value: unknown = null;
  private onChange: (value: unknown) => void = () => undefined;
  writeValue(value: unknown): void {
    this.value = value;
  }
  registerOnChange(fn: (value: unknown) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(): void {
    /* no-op */
  }
  enter(value: unknown): void {
    this.onChange(value);
  }
}

@Component({
  selector: 'cleansia-section',
  standalone: true,
  template: '<ng-content />',
})
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
  buttonSize = input<string>('');
  tooltip = input<string>('');
  ariaLabel = input<string>('');
  onClick = output<void>();
}

@Component({
  selector: 'cleansia-text-input',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextInputStub)],
})
class TextInputStub extends ControlStub {
  label = input<string>('');
  floatVariant = input<string | null>(null);
  dataType = input<string>('text');
  required = input<boolean>(false);
}

@Component({
  selector: 'cleansia-textarea',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextareaStub)],
})
class TextareaStub extends ControlStub {
  label = input<string>('');
  rows = input<number>(3);
}

@Component({
  selector: 'cleansia-select',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => SelectStub)],
})
class SelectStub extends ControlStub {
  label = input<string>('');
  options = input<ICleansiaSelectOption[]>([]);
  floatVariant = input<string | null>(null);
  filter = input<boolean>(false);
  required = input<boolean>(false);
  showClear = input<boolean>(true);
}

@Component({ selector: 'cleansia-employee-documents-section', standalone: true, template: '' })
class DocumentsSectionStub {
  facade = input<unknown>(null);
  employeeId = input<string | undefined>(undefined);
  rejectDocument = output<unknown>();
}

@Component({ selector: 'cleansia-employee-payout-section', standalone: true, template: '' })
class PayoutSectionStub {
  employeeId = input<string>('');
}

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  readonly employee = signal<AdminEmployeeDetail | null>(null);
  readonly loading = signal<boolean>(false);
  readonly editingSection = signal<string | null>(null);
  readonly savingEmployee = signal<boolean>(false);
  readonly editingWeeklyLimit = signal<boolean>(false);
  readonly savingWeeklyLimit = signal<boolean>(false);
  readonly countries = signal<ICleansiaSelectOption[]>([]);
  readonly payConfigSummary = signal<EmployeePayConfigSummaryDto | null>(null);
  readonly loadingPayConfigs = signal<boolean>(false);
  readonly savingPayConfig = signal<boolean>(false);
  readonly bulkApplyingGrade = signal<boolean>(false);
  readonly payConfigDialogOpen = signal<boolean>(false);
  readonly currencies = signal<ICleansiaSelectOption[]>([]);
  readonly hasEntityActions = signal<boolean>(true);
  readonly canApprove = signal<boolean>(false);
  readonly canReject = signal<boolean>(false);
  loadEmployeeDetail = jest.fn();
  loadEmployeePayConfigs = jest.fn();
  loadPayConfigOptions = jest.fn();
  startEditingSection = jest.fn((section: string) => this.editingSection.set(section));
  startEditingWeeklyLimit = jest.fn(() => this.editingWeeklyLimit.set(true));
  cancelEditingWeeklyLimit = jest.fn(() => this.editingWeeklyLimit.set(false));
  setWeeklyOrderLimit = jest.fn();
  connectLegalEntityName = jest.fn();
  ngOnDestroy = jest.fn();
}

/** The page with every shared control stubbed, the text input too unless `textInput` is 'real'. */
async function renderPage(
  facade: FacadeStub,
  textInput: 'stub' | 'real' = 'stub'
): Promise<ComponentFixture<EmployeeDetailComponent>> {
  const stubbed = textInput === 'stub';
  await TestBed.configureTestingModule({
    imports: [EmployeeDetailComponent, TranslateModule.forRoot()],
    providers: [
      provideNoopAnimations(),
      { provide: PermissionService, useValue: { hasPolicy: () => true } },
      { provide: Router, useValue: { navigate: jest.fn() } },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ employeeId: 'employee-1' }) } },
      },
      {
        provide: CountryFieldLabelsService,
        useValue: { labels: signal(null), load: jest.fn() },
      },
    ],
  })
    .overrideComponent(EmployeeDetailComponent, {
      remove: {
        imports: [
          CleansiaButtonComponent,
          CleansiaSectionComponent,
          CleansiaSelectComponent,
          CleansiaTextareaComponent,
          ...(stubbed ? [CleansiaTextInputComponent] : []),
          EmployeeDocumentsSectionComponent,
          EmployeePayoutSectionComponent,
        ],
      },
      add: {
        imports: [
          ButtonStub,
          SectionStub,
          SelectStub,
          TextareaStub,
          ...(stubbed ? [TextInputStub] : []),
          DocumentsSectionStub,
          PayoutSectionStub,
        ],
        providers: [
          { provide: EmployeeDetailFacade, useValue: facade },
          {
            provide: EmployeeDocumentsFacade,
            useValue: {
              destroyed$: new Subject<void>(),
              ngOnDestroy: jest.fn(),
              openRejectDocumentDialog: jest.fn(),
            },
          },
        ],
      },
    })
    .compileComponents();

  return TestBed.createComponent(EmployeeDetailComponent);
}

function employeeDetail(saved: Partial<IAdminEmployeeDetail>): AdminEmployeeDetail {
  return AdminEmployeeDetail.fromJS({
    id: 'employee-1',
    firstName: 'Jana',
    lastName: 'Nová',
    averageRating: 4.8,
    complaintsCount: 0,
    isProfileComplete: true,
    contractStatus: 'Approved',
    ...saved,
  });
}

const REASON_LABEL = 'pages.employee_detail.weekly_order_limit_reason';

describe('EmployeeDetailComponent', () => {
  let fixture: ComponentFixture<EmployeeDetailComponent>;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();
    fixture = await renderPage(facade);
  });

  function render(saved: Partial<IAdminEmployeeDetail>): void {
    facade.employee.set(employeeDetail(saved));
    fixture.detectChanges();
  }

  function reasonNote(): HTMLElement | undefined {
    const notes: HTMLElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.detail-identity__note')
    );
    return notes.find((note) => note.textContent?.includes(REASON_LABEL));
  }

  function click(scope: DebugElement, label: string): void {
    const button = scope
      .queryAll(By.directive(ButtonStub))
      .find((b) => (b.componentInstance as ButtonStub).label() === label);
    if (!button) throw new Error(`no button labelled ${label}`);
    button.triggerEventHandler('onClick');
    fixture.detectChanges();
  }

  function openCapEditor(): void {
    click(fixture.debugElement, 'pages.employee_detail.actions.set_weekly_limit');
  }

  function capPanel(): DebugElement {
    return fixture.debugElement.query(By.css('.detail-actions__panel'));
  }

  function limitBox(): TextInputStub {
    return capPanel().query(By.directive(TextInputStub)).componentInstance;
  }

  function reasonBox(): TextareaStub {
    return capPanel().query(By.directive(TextareaStub)).componentInstance;
  }

  function buttonLabels(): string[] {
    return fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((b) => (b.componentInstance as ButtonStub).label());
  }

  it('offers Approve without Reject when only Approve is open', () => {
    facade.canApprove.set(true);
    facade.canReject.set(false);
    render({ contractStatus: 'Rejected' });

    expect(buttonLabels()).toContain('pages.employee_detail.actions.approve_employee');
    expect(buttonLabels()).not.toContain('pages.employee_detail.actions.reject_employee');
  });

  it('shows the reason the weekly cap was set', () => {
    render({ weeklyOrderLimit: 3, weeklyOrderLimitReason: 'new partner' });

    expect(reasonNote()?.textContent).toContain('new partner');
  });

  it('shows no cap reason when none was recorded', () => {
    render({ weeklyOrderLimit: 3 });

    expect(reasonNote()).toBeUndefined();
  });

  it('opens the cap editor holding the saved reason', () => {
    render({ weeklyOrderLimit: 3, weeklyOrderLimitReason: 'new partner' });
    openCapEditor();

    expect(reasonBox().label()).toBe(REASON_LABEL);
    expect(reasonBox().value).toBe('new partner');
  });

  it('opens the cap editor empty for a cleaner with no saved reason, dropping a discarded draft', () => {
    render({});
    openCapEditor();
    reasonBox().enter('draft that was never saved');
    click(capPanel(), 'global.actions.cancel');

    openCapEditor();

    expect(reasonBox().value).toBe('');
  });

  it('saves the cap with the reason the admin typed', () => {
    render({});
    openCapEditor();
    limitBox().enter(5);
    reasonBox().enter('first month');

    click(capPanel(), 'global.actions.save');

    expect(facade.setWeeklyOrderLimit).toHaveBeenCalledWith(5, 'first month');
  });

  describe('the registered name of a company row', () => {
    function editIdentity(saved: Partial<IAdminEmployeeDetail>): FormControl<string | null> {
      render(saved);
      fixture.componentInstance.onEditSection('employment');
      fixture.detectChanges();
      return fixture.componentInstance.editForm.controls.legalEntityName;
    }

    it('is required of a company row, so one saved without it reads as missing', () => {
      const legalName = editIdentity({ entityType: EmployeeEntityType.LegalEntity, legalEntityName: undefined });

      expect(legalName.hasError('required')).toBe(true);
    });

    it('accepts a company row that carries its name', () => {
      const legalName = editIdentity({ entityType: EmployeeEntityType.LegalEntity, legalEntityName: 'Úklid Novák s.r.o.' });

      expect(legalName.hasValidator(Validators.required)).toBe(true);
      expect(legalName.valid).toBe(true);
    });

    it('is not asked of a natural person', () => {
      const legalName = editIdentity({ entityType: EmployeeEntityType.NaturalPerson });

      expect(legalName.hasValidator(Validators.required)).toBe(false);
      expect(legalName.valid).toBe(true);
    });

    it('follows the entity type the administrator picks while editing', () => {
      const legalName = editIdentity({ entityType: EmployeeEntityType.NaturalPerson });
      const entityType = fixture.componentInstance.editForm.controls.entityType;

      entityType.setValue(EmployeeEntityType.LegalEntity);
      expect(legalName.hasError('required')).toBe(true);

      entityType.setValue(EmployeeEntityType.NaturalPerson);
      expect(legalName.valid).toBe(true);
    });
  });
});

/**
 * The legal-name rule judges the merged row, so a save from any section can be refused for it, and the
 * field exists only in the employment section; the facade opens that section and marks the field.
 */
describe('EmployeeDetailComponent, a company row refused for its registered name', () => {
  let fixture: ComponentFixture<EmployeeDetailComponent>;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();
    fixture = await renderPage(facade, 'real');
    facade.employee.set(employeeDetail({ entityType: EmployeeEntityType.LegalEntity, legalEntityName: undefined }));
    fixture.detectChanges();
  });

  it('hands the legal-name field to the facade', () => {
    expect(facade.connectLegalEntityName).toHaveBeenCalledWith(
      fixture.componentInstance.editForm.controls.legalEntityName
    );
  });

  it('shows the name as required when the facade opens the employment section for it after another section was saved', () => {
    fixture.componentInstance.onEditSection('personal');
    fixture.detectChanges();
    const [legalEntityName] = facade.connectLegalEntityName.mock.calls[0];

    legalEntityName.markAsTouched();
    facade.editingSection.set('employment');
    fixture.detectChanges();

    const field: HTMLElement | null = fixture.nativeElement.querySelector('[formcontrolname="legalEntityName"]');
    expect(field?.querySelector('.cleansia-error-message-container')).not.toBeNull();
  });
});
