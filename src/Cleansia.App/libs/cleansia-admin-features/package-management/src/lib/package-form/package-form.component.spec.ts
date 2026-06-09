/* Test doubles below intentionally mirror the real shared-component and PrimeNG
   selectors so the override-imports swap is binding-compatible under the strict
   template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/component-class-suffix */
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
import { Subject } from 'rxjs';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { MultiSelectModule } from 'primeng/multiselect';
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
}

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string>('');
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
  selector: 'p-multiSelect',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => MultiSelectStub)],
})
class MultiSelectStub extends ControlStub {
  options = input<unknown[]>([]);
  optionLabel = input<string>('');
  placeholder = input<string>('');
  showClear = input<boolean>(false);
  filter = input<boolean>(false);
  filterBy = input<string>('');
  display = input<string>('');
  styleClass = input<string>('');
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
  readonly availableServices = signal<unknown[]>([]);
  readonly weightRows = signal<PackageServiceWeightRow[]>([]);
  readonly derivedGrosses = signal<DerivedServiceGross[]>([]);
  loadLanguages = jest.fn();
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
        remove: {
          imports: [
            CleansiaButtonComponent,
            CleansiaTextInputComponent,
            CleansiaTextareaComponent,
            CleansiaLoaderComponent,
            CleansiaSectionComponent,
            CleansiaTitleComponent,
            MultiSelectModule,
            Tabs,
            TabList,
            Tab,
            TabPanels,
            TabPanel,
          ],
        },
        add: {
          imports: [
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
          ],
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
    facade.errorKey.set('errors.package.invalid_weight');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('errors.package.invalid_weight');
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

  it('delegates service selection to the facade weight sync', () => {
    component.onServiceSelectionChange([
      { id: 'svc-a', name: 'A' } as never,
    ]);
    expect(facade.syncWeightRows).toHaveBeenCalled();
  });
});
