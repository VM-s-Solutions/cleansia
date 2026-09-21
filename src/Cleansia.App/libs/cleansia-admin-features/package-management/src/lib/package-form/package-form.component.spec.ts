/* Test doubles below intentionally mirror the real shared-component and PrimeNG
   selectors so the override-imports swap is binding-compatible under the strict
   template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import {
  Component,
  forwardRef,
  input,
  output,
  signal,
  Type,
} from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { By } from '@angular/platform-browser';
import { of, ReplaySubject, Subject } from 'rxjs';
import { AdminClient } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaMultiselectComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { PackageFormComponent } from './package-form.component';
import { PackageFormFacade } from './package-form.facade';
import { DerivedServiceGross, PackageServiceWeightRow } from './package-form.models';

function valueAccessor(forwardTo: () => Type<unknown>) {
  return {
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(forwardTo),
    multi: true,
  };
}

class ControlStub {
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

@Component({
  selector: 'cleansia-section',
  standalone: true,
  template: '<ng-content />',
})
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
  loading = input<boolean>(false);
  disabled = input<boolean>(false);
  type = input<string>('button');
  onClick = output<void>();
}

@Component({
  selector: 'cleansia-text-input',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextInputStub)],
})
class TextInputStub extends ControlStub {
  id = input<string>('');
  label = input<string>('');
  dataType = input<string>('text');
  required = input<boolean>(false);
  valueChanges = output<string>();
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
  selector: 'cleansia-multiselect',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => MultiSelectStub)],
})
class MultiSelectStub extends ControlStub {
  label = input<string>('');
  options = input<unknown[]>([]);
  maxSelectedLabels = input<number>(3);
  display = input<string>('');
  valueChanges = output<unknown[]>();
}

@Component({ selector: 'p-tabs', standalone: true, template: '<ng-content />' })
class TabsStub {
  value = input<string>('');
}
@Component({
  selector: 'p-tablist',
  standalone: true,
  template: '<ng-content />',
})
class TabListStub {}
@Component({ selector: 'p-tab', standalone: true, template: '<ng-content />' })
class TabStub {
  value = input<string>('');
}
@Component({
  selector: 'p-tabpanels',
  standalone: true,
  template: '<ng-content />',
})
class TabPanelsStub {}
@Component({
  selector: 'p-tabpanel',
  standalone: true,
  template: '<ng-content />',
})
class TabPanelStub {
  value = input<string>('');
}

const STUB_IMPORTS = [
  ButtonStub,
  TextInputStub,
  TextareaStub,
  LoaderStub,
  SectionStub,
  TitleStub,
  MultiSelectStub,
  TabsStub,
  TabListStub,
  TabStub,
  TabPanelsStub,
  TabPanelStub,
];

const REAL_IMPORTS = [
  CleansiaButtonComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
  CleansiaMultiselectComponent,
  Tabs,
  TabList,
  Tab,
  TabPanels,
  TabPanel,
];

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly pkg = signal<unknown>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);
  readonly languages = signal<{ code: string; name: string }[]>([
    { code: 'en', name: 'English' },
  ]);
  readonly currencies = signal<
    {
      code: string;
      symbol: string;
      name: string;
      isDefault: boolean;
      isActive: boolean;
    }[]
  >([
    {
      code: 'CZK',
      symbol: 'Kc',
      name: 'Czech koruna',
      isDefault: true,
      isActive: true,
    },
  ]);
  readonly defaultCurrencyCode = signal<string | null>('CZK');
  readonly availableServices = signal<unknown[]>([]);
  readonly serviceOptions = signal<{ label: string; value: string }[]>([]);
  readonly weightRows = signal<PackageServiceWeightRow[]>([]);
  readonly derivedGrosses = signal<DerivedServiceGross[]>([]);
  loadLanguages = jest.fn();
  loadCurrencies = jest.fn();
  loadAvailableServices = jest.fn();
  loadPackage = jest.fn();
  setPrice = jest.fn();
  syncWeightRows = jest.fn();
  setWeight = jest.fn();
  buildServiceWeights = jest.fn(() => ({}));
  updatePackage = jest.fn();
  createPackage = jest.fn();
  navigateBack = jest.fn();
}

describe('PackageFormComponent', () => {
  let fixture: ComponentFixture<PackageFormComponent>;
  let component: PackageFormComponent;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [PackageFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { data: {}, paramMap: { get: () => null } },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(PackageFormComponent, {
        remove: { imports: REAL_IMPORTS },
        add: {
          imports: STUB_IMPORTS,
          providers: [{ provide: PackageFormFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(PackageFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('uses OnPush change detection', () => {
    const meta = (
      PackageFormComponent as unknown as { ɵcmp: { onPush: boolean } }
    ).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('renders the loading state while the package is loading', () => {
    facade.loading.set(true);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.directive(LoaderStub))).toBeTruthy();
  });

  it('renders the form (loaded state) when not loading', () => {
    facade.loading.set(false);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.directive(LoaderStub))).toBeNull();
    expect(
      fixture.debugElement.query(By.directive(MultiSelectStub))
    ).toBeTruthy();
  });

  it('renders the error state when the facade exposes an error key', () => {
    facade.errorKey.set('api.package.invalid_weight');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('api.package.invalid_weight');
  });

  it('renders a weight input and derived gross per included service', () => {
    facade.weightRows.set([{ id: 'svc-a', name: 'A', weight: 3 }]);
    facade.derivedGrosses.set([
      { id: 'svc-a', name: 'A', weight: 3, gross: 75 },
    ]);
    fixture.detectChanges();

    const weightInputs = fixture.debugElement.queryAll(
      By.directive(TextInputStub)
    );
    const weightField = weightInputs.find(
      (de) => (de.componentInstance as TextInputStub).id() === 'weight-svc-a'
    );
    expect(weightField).toBeTruthy();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('pages.package_form.derived_gross');
    expect(text).toContain('75');
  });

  it('delegates a weight change to the facade', () => {
    component.onWeightChange('svc-a', '4');
    expect(facade.setWeight).toHaveBeenCalledWith('svc-a', 4);
  });

  it('syncs the weight rows for the services the admin picked', () => {
    facade.availableServices.set([
      { id: 'svc-a', name: 'A' },
      { id: 'svc-b', name: 'B' },
    ]);

    component.onServiceSelectionChange(['svc-b']);

    expect(facade.syncWeightRows).toHaveBeenCalledWith(
      [{ id: 'svc-b', name: 'B' }],
      undefined
    );
  });

  it('never collapses the chips: the limit sits above the option count', () => {
    facade.serviceOptions.set([
      { label: 'A', value: 'svc-a' },
      { label: 'B', value: 'svc-b' },
      { label: 'C', value: 'svc-c' },
    ]);
    fixture.detectChanges();

    const multiselect = fixture.debugElement.query(By.directive(MultiSelectStub))
      .componentInstance as MultiSelectStub;
    expect(multiselect.maxSelectedLabels()).toBe(4);
  });

  /**
   * WHO OWNS THE TRANSLATION TAB. `p-tabs` exposes `value` as a two-way `model()`, and this was bound
   * ONE-WAY to `facade.languages()[0].code`. Two things were wrong with that and only one of them was
   * a crash:
   *
   *  - `[0]` on an empty array is `undefined.code`, a TypeError that takes the form down — and the
   *    facade's own `catchError` sets the list to `[]` on any failed read, so a network blip was
   *    enough. The stub below seeds a language, which is exactly why no test could see it.
   *  - the component could not read the selection at all, and Angular wrote the expression back over
   *    the user's choice whenever it changed.
   */
  it('renders with no languages rather than dying on the tab strip', () => {
    facade.languages.set([]);

    expect(() => fixture.detectChanges()).not.toThrow();
    expect(fixture.nativeElement.querySelectorAll('p-tab')).toHaveLength(0);
  });

  it('opens the first language once the list arrives', () => {
    // The stub seeds one language before the fixture is built, so the defaulting effect has already
    // run by the time the test body starts — which is the behaviour, not an artefact: it opens the
    // first language the moment one exists and does not wait to be asked.
    expect(component.activeLanguage()).toBe('en');
  });

  it("keeps the user's chosen language when the list re-emits", () => {
    facade.languages.set([
      { code: 'cs', name: 'Cestina' },
      { code: 'en', name: 'English' },
    ]);
    fixture.detectChanges();

    component.onLanguageTabChange('en');
    fixture.detectChanges();

    // A reload, a retry, a second loadLanguages(). The old binding re-derived from [0] and would
    // have dragged the tab back to Czech.
    facade.languages.set([
      { code: 'cs', name: 'Cestina' },
      { code: 'en', name: 'English' },
    ]);
    fixture.detectChanges();

    expect(component.activeLanguage()).toBe('en');
  });

});

/**
 * The edit route with the REAL facade. The stubbed facade above makes `syncWeightRows` a no-op, so
 * only a run against the real one can see the load effect feed its own dependency: it read
 * `weightRows()` and set a fresh array in the same tracked run, re-dirtying itself forever and
 * leaving /package-management/:id/edit without a rendered form.
 */
describe('PackageFormComponent (edit mode, real facade)', () => {
  const PACKAGE_ID = 'pkg-1';
  const SERVICE_A = { id: 'svc-a', name: 'Windows' };
  const SERVICE_B = { id: 'svc-b', name: 'Floors' };

  let fixture: ComponentFixture<PackageFormComponent>;
  let component: PackageFormComponent;
  let facade: PackageFormFacade;
  let services$: ReplaySubject<{ data: typeof SERVICE_A[]; total: number }>;

  /**
   * A self-dirtying effect spins synchronously with no cap, so a regression would hang the run
   * rather than fail it. The cap turns the hang into an assertion.
   */
  function capWeightSyncs(limit: number): jest.SpyInstance {
    const original = facade.syncWeightRows.bind(facade);
    const spy = jest.spyOn(facade, 'syncWeightRows');
    spy.mockImplementation((selected, source) => {
      if (spy.mock.calls.length > limit) {
        throw new Error('the package load effect re-ran on its own write');
      }
      original(selected, source);
    });
    return spy;
  }

  beforeEach(async () => {
    services$ = new ReplaySubject(1);
    const adminClient = {
      adminPackageClient: {
        details: jest.fn().mockReturnValue(
          of({
            id: PACKAGE_ID,
            name: 'Move-out bundle',
            description: 'desc',
            tagline: 'Handover day',
            isPopular: true,
            prices: { CZK: 1000 },
            includedServices: [
              { ...SERVICE_A, priceWeight: 3 },
              { ...SERVICE_B, priceWeight: 1 },
            ],
            translations: {},
          })
        ),
        update: jest.fn(),
        create: jest.fn(),
      },
      adminLanguageClient: { getOverview: jest.fn().mockReturnValue(of([])) },
      adminCurrencyClient: {
        getOverview: jest.fn().mockReturnValue(
          of([
            {
              code: 'CZK',
              symbol: 'Kc',
              name: 'Czech koruna',
              isDefault: true,
              isActive: true,
            },
          ])
        ),
      },
      adminServiceClient: {
        getPaged: jest.fn().mockReturnValue(services$),
      },
    };

    await TestBed.configureTestingModule({
      imports: [PackageFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { mode: 'edit' },
              paramMap: { get: () => PACKAGE_ID },
            },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminClient, useValue: adminClient },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
      ],
    })
      .overrideComponent(PackageFormComponent, {
        remove: { imports: REAL_IMPORTS },
        add: { imports: STUB_IMPORTS },
      })
      .compileComponents();

    fixture = TestBed.createComponent(PackageFormComponent);
    component = fixture.componentInstance;
    facade = fixture.debugElement.injector.get(PackageFormFacade);
  });

  it('renders the loaded package instead of looping on its own weight rows', () => {
    const sync = capWeightSyncs(3);
    services$.next({ data: [SERVICE_A, SERVICE_B], total: 2 });

    expect(() => fixture.detectChanges()).not.toThrow();

    expect(sync).toHaveBeenCalledTimes(1);
    expect(fixture.debugElement.query(By.directive(LoaderStub))).toBeNull();
    expect(component.form.controls.name.value).toBe('Move-out bundle');
    expect(component.form.controls.isPopular.value).toBe(true);
    expect(component.form.controls.serviceIds.value).toEqual(['svc-a', 'svc-b']);
    expect(facade.weightRows()).toEqual([
      { id: 'svc-a', name: 'Windows', weight: 3 },
      { id: 'svc-b', name: 'Floors', weight: 1 },
    ]);
    expect(facade.derivedGrosses().map((g) => g.gross)).toEqual([750, 250]);
  });

  it('builds the weight rows once the service list lands after the package', () => {
    capWeightSyncs(4);
    fixture.detectChanges();
    expect(component.form.controls.serviceIds.value).toEqual(['svc-a', 'svc-b']);
    expect(facade.weightRows()).toEqual([]);

    services$.next({ data: [SERVICE_A, SERVICE_B], total: 2 });
    fixture.detectChanges();

    expect(facade.weightRows().map((r) => r.weight)).toEqual([3, 1]);
  });

  it('keeps a weight the admin changed', () => {
    capWeightSyncs(3);
    services$.next({ data: [SERVICE_A, SERVICE_B], total: 2 });
    fixture.detectChanges();

    component.onWeightChange('svc-a', 5);
    fixture.detectChanges();

    expect(facade.weightRows().map((r) => r.weight)).toEqual([5, 1]);
    expect(facade.derivedGrosses().map((g) => g.gross)).toEqual([
      833.33, 166.67,
    ]);
  });
});

