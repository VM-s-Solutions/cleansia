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
  FormControl,
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
import {
  ServiceFormData,
  ServiceFormFacade,
  ServicePriceInput,
} from './service-form.facade';

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
    estimatedTime: [0, [Validators.required, Validators.min(0)]],
    categoryId: ['', [Validators.required]],
    translations: this.fb.nonNullable.group({}),
    prices: this.fb.nonNullable.group({}),
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

  private currenciesLoadEffect = effect(() => {
    const currencies = this.facade.currencies();
    if (currencies.length > 0) {
      this.buildPriceFormGroups(currencies);
      // The service may have loaded before the currency list did, in which case populateForm had
      // no blocks to write into. Replay it now rather than leaving the amounts blank.
      const service = this.facade.service();
      if (service && this.isEditMode()) {
        this.patchPrices(service.prices);
      }
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    this.facade.loadLanguages();
    this.facade.loadCurrencies();
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

  /**
   * One block per currency, built once the list arrives. Blank rather than zero: a service that has
   * never been priced in a currency is not the same as one priced at nothing, and onSave leans on
   * exactly that distinction to decide which keys to send.
   */
  private buildPriceFormGroups(currencies: { code: string }[]): void {
    const pricesGroup = this.form.get('prices') as FormGroup;

    for (const currency of currencies) {
      if (!pricesGroup.contains(currency.code)) {
        pricesGroup.addControl(
          currency.code,
          this.fb.group({
            basePrice: new FormControl<number | null>(null, [
              Validators.min(0),
            ]),
            perRoomPrice: new FormControl<number | null>(null, [
              Validators.min(0),
            ]),
          })
        );
      }
    }
  }

  private patchPrices(
    prices?: { [key: string]: { basePrice?: number; perRoomPrice?: number } }
  ): void {
    if (!prices) {
      return;
    }

    const pricesGroup = this.form.get('prices') as FormGroup;
    for (const [code, price] of Object.entries(prices)) {
      pricesGroup.get(code)?.patchValue({
        basePrice: price.basePrice ?? null,
        perRoomPrice: price.perRoomPrice ?? null,
      });
    }
  }

  private populateForm(service: {
    name?: string;
    description?: string;
    prices?: { [key: string]: { basePrice?: number; perRoomPrice?: number } };
    estimatedTime?: number;
    translations?: { [key: string]: { name?: string; description?: string } };
  }): void {
    this.form.patchValue({
      name: service.name ?? '',
      description: service.description ?? '',
      estimatedTime: service.estimatedTime ?? 0,
    });

    this.patchPrices(service.prices);

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

    // ONLY THE CURRENCIES THE ADMIN ACTUALLY FILLED IN. A blank block means "not priced in this
    // currency", and the backend upserts a row for every key it receives -- so sending a blank one
    // as 0 would put the service on sale for nothing rather than leaving it off sale.
    const pricesValue = formValue.prices as {
      [key: string]: { basePrice: number | null; perRoomPrice: number | null };
    };
    const prices: { [key: string]: ServicePriceInput } = {};
    for (const [code, price] of Object.entries(pricesValue)) {
      if (price.basePrice === null && price.perRoomPrice === null) {
        continue;
      }
      prices[code] = {
        basePrice: price.basePrice ?? 0,
        perRoomPrice: price.perRoomPrice ?? 0,
      };
    }

    const data: ServiceFormData = {
      name: formValue.name,
      description: formValue.description,
      prices,
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
