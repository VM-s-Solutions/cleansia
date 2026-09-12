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
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { ExtraFormData, ExtraFormFacade } from './extra-form.facade';

@Component({
  selector: 'cleansia-admin-extra-form',
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
    CleansiaTitleComponent,
  ],
  templateUrl: './extra-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ExtraFormFacade],
})
export class ExtraFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(ExtraFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  /**
   * Which translation tab is open — a signal the USER's click owns. Defaulted from the first
   * language once, by the effect below, rather than derived on every read; see the service form
   * for why a one-way binding here took the strip's owner away.
   */
  readonly activeLanguage = signal<string>('');

  onLanguageTabChange(value: string | number | undefined): void {
    if (typeof value === 'string') {
      this.activeLanguage.set(value);
    }
  }

  constructor() {
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
      ? this.translate.instant('pages.extra_form.edit_title')
      : this.translate.instant('pages.extra_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    slug: [
      '',
      [
        Validators.required,
        Validators.maxLength(50),
        Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/),
      ],
    ],
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    displayOrder: [0, [Validators.required, Validators.min(0)]],
    translations: this.fb.nonNullable.group({}),
    prices: this.fb.nonNullable.group({}),
  });

  private extraLoadEffect = effect(() => {
    const extra = this.facade.extra();
    if (extra && this.isEditMode()) {
      this.populateForm(extra);
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
      // The extra may have loaded before the currency list did, in which case populateForm had
      // no blocks to write into. Replay it now rather than leaving the amounts blank.
      const extra = this.facade.extra();
      if (extra && this.isEditMode()) {
        this.patchPrices(extra.prices);
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

    if (this.isEditMode()) {
      // Shown, never sent: the slug is fixed at creation and UpdateExtraCommand has no property for it.
      this.form.controls.slug.disable();

      const extraId = this.route.snapshot.paramMap.get('extraId');
      if (extraId) {
        this.facade.loadExtra(extraId);
      } else {
        this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT]);
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
   * One block per currency -- see the service form's twin for why they are blank, not zero.
   */
  private buildPriceFormGroups(currencies: { code: string }[]): void {
    const pricesGroup = this.form.get('prices') as FormGroup;

    for (const currency of currencies) {
      if (!pricesGroup.contains(currency.code)) {
        pricesGroup.addControl(
          currency.code,
          new FormControl<number | null>(null, [Validators.min(0)])
        );
      }
    }
  }

  private patchPrices(prices?: { [key: string]: number }): void {
    if (!prices) {
      return;
    }

    const pricesGroup = this.form.get('prices') as FormGroup;
    for (const [code, price] of Object.entries(prices)) {
      pricesGroup.get(code)?.setValue(price ?? null);
    }
  }

  private populateForm(extra: {
    slug?: string;
    name?: string;
    description?: string;
    displayOrder?: number;
    prices?: { [key: string]: number };
    translations?: { [key: string]: { name?: string; description?: string } };
  }): void {
    this.form.patchValue({
      slug: extra.slug ?? '',
      name: extra.name ?? '',
      description: extra.description ?? '',
      displayOrder: extra.displayOrder ?? 0,
    });

    this.patchPrices(extra.prices);

    if (extra.translations) {
      const translationsGroup = this.form.get('translations') as FormGroup;
      for (const [langCode, translation] of Object.entries(
        extra.translations
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

    for (const [langCode, trans] of Object.entries(translationsValue)) {
      translations[langCode] = {
        name: trans.name ?? '',
        description: trans.description ?? '',
      };
    }

    // ONLY THE CURRENCIES THE ADMIN ACTUALLY FILLED IN -- see the service form for why a blank
    // block is omitted rather than sent as zero.
    const pricesValue = formValue.prices as { [key: string]: number | null };
    const prices: { [key: string]: number } = {};
    for (const [code, price] of Object.entries(pricesValue)) {
      if (price === null) {
        continue;
      }
      prices[code] = price;
    }

    const data: ExtraFormData = {
      slug: formValue.slug,
      name: formValue.name,
      description: formValue.description,
      displayOrder: formValue.displayOrder,
      prices,
      translations,
    };

    if (this.isEditMode()) {
      const extraId = this.route.snapshot.paramMap.get('extraId');
      if (extraId) {
        this.facade.updateExtra(extraId, data);
      }
    } else {
      this.facade.createExtra(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}
