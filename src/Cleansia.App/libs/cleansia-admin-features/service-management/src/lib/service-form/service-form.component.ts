import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
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
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { ServiceFormData, ServiceFormFacade } from './service-form.facade';

@Component({
  selector: 'cleansia-admin-service-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './service-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ServiceFormFacade],
})
export class ServiceFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(ServiceFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  /**
   * Which translation tab is open — a signal the USER's click owns.
   *
   * PrimeNG's `p-tabs` exposes `value` as a two-way `model()`. This was bound ONE-WAY to
   * `facade.languages()[0].code`, which gave the strip no owner at all: the component could not read
   * the selection, and Angular wrote the expression back over it whenever the expression changed —
   * which it does when the language list arrives, and would do again on any reload. It also
   * dereferenced `[0]` on a list the facade's own `catchError` sets to `[]` on a failed read, so a
   * network blip was a TypeError that took the form down.
   *
   * Defaulted from the first language once, by the effect below, rather than derived on every read.
   * Same shape the three other tab strips in this app already use.
   */
  readonly activeLanguage = signal<string>('');

  onLanguageTabChange(value: string | number | undefined): void {
    if (typeof value === 'string') {
      this.activeLanguage.set(value);
    }
  }

  constructor() {
    // Open the first language once it is known, and never again: re-deriving it would drag the
    // user's tab back every time the list re-emitted.
    effect(() => {
      const first = this.facade.languages()[0]?.code;
      if (first && !this.activeLanguage()) {
        this.activeLanguage.set(first);
      }
    });

  }


  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.service_form.edit_title')
      : this.translate.instant('pages.service_form.create_title')
  );

  /** Map facade.categories() into the select-component's option shape. */
  readonly categoryOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.categories().map((c) => ({ label: c.name, value: c.id }))
  );

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    basePrice: [0, [Validators.required, Validators.min(0)]],
    perRoomPrice: [0, [Validators.required, Validators.min(0)]],
    estimatedTime: [0, [Validators.required, Validators.min(0)]],
    categoryId: ['', [Validators.required]],
    translations: this.fb.nonNullable.group({}),
  });

  private serviceLoadEffect = effect(() => {
    const service = this.facade.service();
    if (service && this.isEditMode()) {
      this.populateForm(service);
    }
  });

  private languagesLoadEffect = effect(() => {
    const languages = this.facade.languages();
    if (languages.length > 0) {
      this.buildTranslationFormGroups(languages);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    this.facade.loadLanguages();
    this.facade.loadCategories();

    if (this.isEditMode()) {
      const serviceId = this.route.snapshot.paramMap.get('serviceId');
      if (serviceId) {
        this.facade.loadService(serviceId);
      } else {
        this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT]);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private buildTranslationFormGroups(
    languages: { code: string; name: string }[]
  ): void {
    const translationsGroup = this.form.get('translations') as FormGroup;

    for (const lang of languages) {
      if (!translationsGroup.contains(lang.code)) {
        translationsGroup.addControl(
          lang.code,
          this.fb.nonNullable.group({
            name: ['', [Validators.required, Validators.maxLength(100)]],
            description: ['', [Validators.maxLength(500)]],
          })
        );
      }
    }
  }

  private populateForm(service: {
    name?: string;
    description?: string;
    basePrice?: number;
    perRoomPrice?: number;
    estimatedTime?: number;
    translations?: { [key: string]: { name?: string; description?: string } };
  }): void {
    this.form.patchValue({
      name: service.name ?? '',
      description: service.description ?? '',
      basePrice: service.basePrice ?? 0,
      perRoomPrice: service.perRoomPrice ?? 0,
      estimatedTime: service.estimatedTime ?? 0,
    });

    if (service.translations) {
      const translationsGroup = this.form.get('translations') as FormGroup;
      for (const [langCode, translation] of Object.entries(
        service.translations
      )) {
        if (translationsGroup.contains(langCode)) {
          translationsGroup.get(langCode)?.patchValue({
            name: translation.name ?? '',
            description: translation.description ?? '',
          });
        }
      }
    }
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();
    const translations: {
      [key: string]: { name: string; description: string };
    } = {};

    const translationsValue = formValue.translations as {
      [key: string]: { name: string; description: string };
    };

    // Include all translations (required for all languages)
    for (const [langCode, trans] of Object.entries(translationsValue)) {
      translations[langCode] = {
        name: trans.name ?? '',
        description: trans.description ?? '',
      };
    }

    const data: ServiceFormData = {
      name: formValue.name,
      description: formValue.description,
      basePrice: formValue.basePrice,
      perRoomPrice: formValue.perRoomPrice,
      estimatedTime: formValue.estimatedTime,
      categoryId: formValue.categoryId,
      translations,
    };

    if (this.isEditMode()) {
      const serviceId = this.route.snapshot.paramMap.get('serviceId');
      if (serviceId) {
        this.facade.updateService(serviceId, data);
      }
    } else {
      this.facade.createService(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}
