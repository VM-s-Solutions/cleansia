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
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
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
    CleansiaTitleComponent,
    CleansiaLanguageSwitcherComponent,
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

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.service_form.edit_title')
      : this.translate.instant('pages.service_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    basePrice: [0, [Validators.required, Validators.min(0)]],
    perRoomPrice: [0, [Validators.required, Validators.min(0)]],
    estimatedTime: [0, [Validators.required, Validators.min(0)]],
    translations: this.fb.group({}),
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
          this.fb.group({
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
