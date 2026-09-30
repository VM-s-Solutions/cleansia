/* Test doubles below intentionally mirror the real shared-component and PrimeNG
   selectors so the override-imports swap is binding-compatible under the strict
   template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output, signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminServiceDetailDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { Subject } from 'rxjs';
import { ServiceFormComponent } from './service-form.component';
import {
  CategoryOption,
  CurrencyOption,
  LanguageOption,
  ServiceFormFacade,
} from './service-form.facade';

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
  label = input<string>('');
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
  required = input<boolean>(false);
  showClear = input<boolean>(true);
}

@Component({ selector: 'p-tabs', standalone: true, template: '<ng-content />' })
class TabsStub {
  value = input<string>('');
}
@Component({ selector: 'p-tablist', standalone: true, template: '<ng-content />' })
class TabListStub {}
@Component({ selector: 'p-tab', standalone: true, template: '<ng-content />' })
class TabStub {
  value = input<string>('');
}
@Component({ selector: 'p-tabpanels', standalone: true, template: '<ng-content />' })
class TabPanelsStub {}
@Component({ selector: 'p-tabpanel', standalone: true, template: '<ng-content />' })
class TabPanelStub {
  value = input<string>('');
}

const STUB_IMPORTS = [
  ButtonStub,
  TextInputStub,
  TextareaStub,
  SelectStub,
  LoaderStub,
  SectionStub,
  TitleStub,
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
  CleansiaSelectComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
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
  readonly service = signal<AdminServiceDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly languages = signal<LanguageOption[]>([]);
  readonly currencies = signal<CurrencyOption[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  loadService = jest.fn();
  loadLanguages = jest.fn();
  loadCurrencies = jest.fn();
  loadCategories = jest.fn();
  createService = jest.fn();
  updateService = jest.fn();
  navigateBack = jest.fn();
}

describe('ServiceFormComponent (edit mode)', () => {
  const SERVICE_ID = 'svc-1';

  let fixture: ComponentFixture<ServiceFormComponent>;
  let component: ServiceFormComponent;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [ServiceFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { mode: 'edit' },
              paramMap: { get: (key: string) => (key === 'serviceId' ? SERVICE_ID : null) },
            },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(ServiceFormComponent, {
        remove: { imports: REAL_IMPORTS },
        add: {
          imports: STUB_IMPORTS,
          providers: [{ provide: ServiceFormFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(ServiceFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('saves the minutes per room the service already has instead of resetting them to 0', () => {
    facade.service.set(
      AdminServiceDetailDto.fromJS({
        id: SERVICE_ID,
        name: 'Deep clean',
        description: 'Full property deep clean',
        categoryId: 'cat-1',
        estimatedTime: 180,
        minutesPerRoom: 15,
        prices: {},
        translations: {},
      })
    );
    fixture.detectChanges();

    expect(component.form.controls.minutesPerRoom.value).toBe(15);

    component.form.controls.categoryId.setValue('cat-1');
    component.onSave();

    expect(facade.updateService).toHaveBeenCalledWith(
      SERVICE_ID,
      expect.objectContaining({ estimatedTime: 180, minutesPerRoom: 15 })
    );
  });
});
