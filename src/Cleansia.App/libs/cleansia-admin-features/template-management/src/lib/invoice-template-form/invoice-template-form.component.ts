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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaFileComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { FileTransformationUtils } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InvoiceTemplateFormFacade, InvoiceTemplateFormData } from './invoice-template-form.facade';

@Component({
  selector: 'lib-invoice-template-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaFileComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './invoice-template-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [InvoiceTemplateFormFacade],
})
export class InvoiceTemplateFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(InvoiceTemplateFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');
  private readonly templateIdSignal = signal<string | null>(null);

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.template_management.edit_invoice_template')
      : this.translate.instant('pages.template_management.create_invoice_template')
  );

  readonly form = this.fb.nonNullable.group({
    templateName: ['', [Validators.required, Validators.maxLength(100)]],
    countryId: ['', Validators.required],
    languageId: ['', Validators.required],
    templateFile: [[] as File[], Validators.required],
    description: ['', Validators.maxLength(1000)],
  });

  readonly countryOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade
      .countries()
      .filter((c) => c.id)
      .map((c) => ({
        value: c.id!,
        label: c.name ?? c.isoCode ?? '',
      }))
  );

  readonly languageOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade
      .languages()
      .filter((l) => l.id)
      .map((l) => ({
        value: l.id!,
        label: l.name ?? l.code ?? '',
      }))
  );

  readonly currentFileName = computed(() => {
    const template = this.facade.template();
    if (template?.blobUrl) {
      const parts = template.blobUrl.split('/');
      return parts[parts.length - 1];
    }
    return null;
  });

  readonly currentVersion = computed(() => {
    const template = this.facade.template();
    return template?.version ?? null;
  });

  private templateLoadEffect = effect(() => {
    const template = this.facade.template();
    if (template && this.isEditMode()) {
      this.populateForm(template);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    this.facade.loadCountries();
    this.facade.loadLanguages();

    if (this.isEditMode()) {
      const templateId = this.route.snapshot.paramMap.get('templateId');
      if (templateId) {
        this.templateIdSignal.set(templateId);
        this.facade.loadTemplate(templateId);
      } else {
        this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, 'invoice-templates']);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private populateForm(template: {
    templateName?: string;
    countryId?: string;
    languageId?: string;
    description?: string | null;
  }): void {
    this.form.patchValue({
      templateName: template.templateName ?? '',
      countryId: template.countryId ?? '',
      languageId: template.languageId ?? '',
      description: template.description ?? '',
    });

    // Disable country and language in edit mode
    // Make file optional in edit mode (only required for new templates)
    if (this.isEditMode()) {
      this.form.get('countryId')?.disable();
      this.form.get('languageId')?.disable();
      this.form.get('templateFile')?.clearValidators();
      this.form.get('templateFile')?.updateValueAndValidity();
    }
  }

  downloadCurrentFile(): void {
    const templateId = this.templateIdSignal();
    if (templateId) {
      this.facade.downloadTemplate(templateId, this.currentFileName() ?? undefined);
    }
  }

  openInNewTab(): void {
    const templateId = this.templateIdSignal();
    if (templateId) {
      this.facade.openTemplateInNewTab(templateId);
    }
  }

  async onSave(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();
    const files = formValue.templateFile;
    const file = files.length > 0 ? files[0] : null;

    let fileData: string | null = null;
    let fileName: string | null = null;
    let contentType: string | null = null;

    if (file) {
      fileData = await FileTransformationUtils.fileToBase64(file);
      fileName = file.name;
      contentType = file.type || 'text/html';
    }

    const data: InvoiceTemplateFormData = {
      templateName: formValue.templateName,
      countryId: formValue.countryId,
      languageId: formValue.languageId,
      description: formValue.description || null,
      fileName,
      contentType,
      fileData,
    };

    const templateId = this.templateIdSignal();
    if (this.isEditMode() && templateId) {
      this.facade.updateTemplate(templateId, data);
    } else {
      this.facade.createTemplate(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}
