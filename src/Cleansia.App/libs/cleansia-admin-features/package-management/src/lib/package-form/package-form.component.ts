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
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ServiceListItem } from '@cleansia/admin-services';
import { CleansiaAdminRoute } from '@cleansia/services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
  CleansiaCheckboxComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MultiSelectModule } from 'primeng/multiselect';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { takeUntil } from 'rxjs';
import { PackageFormData, PackageFormFacade } from './package-form.facade';

@Component({
  selector: 'cleansia-admin-package-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TranslatePipe,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    MultiSelectModule,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaCheckboxComponent,
    CleansiaTextareaComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './package-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PackageFormFacade],
})
export class PackageFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(PackageFormFacade);

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
      ? this.translate.instant('pages.package_form.edit_title')
      : this.translate.instant('pages.package_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    // The one short line the customer-facing package card leads with, above the
    // name. Sixty characters is what fits that slot; the backend enforces it too.
    tagline: ['', [Validators.maxLength(60)]],
    isPopular: [false],
    price: [0, [Validators.required, Validators.min(0)]],
    serviceIds: [[] as string[]],
    translations: this.fb.nonNullable.group({}),
  });

  readonly selectedServices = signal<ServiceListItem[]>([]);

  private packageLoadEffect = effect(() => {
    const pkg = this.facade.pkg();
    if (pkg && this.isEditMode()) {
      this.populateForm(pkg);
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
    this.facade.loadAvailableServices();

    this.facade.setPrice(Number(this.form.controls.price.value));
    this.form.controls.price.valueChanges
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe((price) => this.facade.setPrice(Number(price)));

    if (this.isEditMode()) {
      const packageId = this.route.snapshot.paramMap.get('packageId');
      if (packageId) {
        this.facade.loadPackage(packageId);
      } else {
        this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT]);
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
            tagline: ['', [Validators.maxLength(60)]],
          })
        );
      }
    }
  }

  private populateForm(pkg: {
    name?: string;
    description?: string;
    price?: number;
    includedServices?: {
      id?: string;
      name?: string;
      description?: string;
      priceWeight?: number;
    }[];
    tagline?: string | undefined;
    isPopular?: boolean;
    translations?: {
      [key: string]: { name?: string; description?: string; tagline?: string };
    };
  }): void {
    this.form.patchValue({
      name: pkg.name ?? '',
      description: pkg.description ?? '',
      tagline: pkg.tagline ?? '',
      isPopular: pkg.isPopular ?? false,
      price: pkg.price ?? 0,
      serviceIds:
        pkg.includedServices
          ?.map((s) => s.id)
          .filter((id): id is string => !!id) ?? [],
    });

    this.facade.setPrice(pkg.price ?? 0);

    // Set selected services for multiselect
    if (pkg.includedServices) {
      const availableServices = this.facade.availableServices();
      const selected = availableServices.filter((s) =>
        pkg.includedServices!.some((is) => is.id === s.id)
      );
      this.selectedServices.set(selected);
      this.facade.syncWeightRows(
        selected
          .filter((s): s is ServiceListItem & { id: string } => Boolean(s.id))
          .map((s) => ({ id: s.id, name: s.name ?? '' })),
        pkg.includedServices
      );
    }

    if (pkg.translations) {
      const translationsGroup = this.form.get('translations') as FormGroup;
      for (const [langCode, translation] of Object.entries(
        pkg.translations
      )) {
        if (translationsGroup.contains(langCode)) {
          translationsGroup.get(langCode)?.patchValue({
            name: translation.name ?? '',
            description: translation.description ?? '',
            tagline: translation.tagline ?? '',
          });
        }
      }
    }
  }

  onServiceSelectionChange(selected: ServiceListItem[]): void {
    this.selectedServices.set(selected);
    const serviceIds = selected
      .map((s) => s.id)
      .filter((id): id is string => !!id);
    this.form.patchValue({ serviceIds });
    this.facade.syncWeightRows(
      selected
        .filter((s): s is ServiceListItem & { id: string } => Boolean(s.id))
        .map((s) => ({ id: s.id, name: s.name ?? '' })),
      this.facade.pkg()?.includedServices
    );
  }

  onWeightChange(serviceId: string, value: string | number): void {
    this.facade.setWeight(serviceId, Number(value));
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();
    const translations: {
      [key: string]: { name: string; description: string; tagline: string };
    } = {};

    const translationsValue = formValue.translations as {
      [key: string]: { name: string; description: string; tagline: string };
    };

    // Include all translations (required for all languages)
    for (const [langCode, trans] of Object.entries(translationsValue)) {
      translations[langCode] = {
        name: trans.name ?? '',
        description: trans.description ?? '',
        tagline: trans.tagline ?? '',
      };
    }

    const data: PackageFormData = {
      name: formValue.name,
      description: formValue.description,
      tagline: formValue.tagline,
      isPopular: formValue.isPopular,
      price: formValue.price,
      serviceIds: formValue.serviceIds,
      translations,
    };

    if (this.isEditMode()) {
      const packageId = this.route.snapshot.paramMap.get('packageId');
      if (packageId) {
        this.facade.updatePackage(packageId, data);
      }
    } else {
      this.facade.createPackage(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}