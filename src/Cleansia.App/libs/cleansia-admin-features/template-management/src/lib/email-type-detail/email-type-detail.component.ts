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
import { ActivatedRoute } from '@angular/router';
import {
  EmailTemplateKeyValueDto,
  EmailTranslationByLanguageDto,
  EmailType,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { TooltipModule } from 'primeng/tooltip';
import { EmailTypeDetailFacade } from './email-type-detail.facade';

@Component({
  selector: 'lib-email-type-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    ConfirmDialogModule,
    DialogModule,
    TooltipModule,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    CleansiaButtonComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './email-type-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [EmailTypeDetailFacade, ConfirmationService],
})
export class EmailTypeDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(EmailTypeDetailFacade);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly confirmationService = inject(ConfirmationService);

  private emailType: EmailType | null = null;
  readonly forms = new Map<string, FormGroup>();
  testEmailForm!: FormGroup;
  addTranslationForm!: FormGroup;
  showTestEmailDialog = signal(false);
  showAddTranslationDialog = signal(false);
  readonly savingKey = signal<string | null>(null);
  readonly deletingKey = signal<string | null>(null);
  private currentLanguageForAdd: EmailTranslationByLanguageDto | null = null;

  readonly pageTitle = computed(() => {
    const detail = this.facade.emailTypeDetail();
    return (
      detail?.displayName ??
      this.translate.instant('pages.template_management.edit_email_template')
    );
  });

  readonly languages = computed(() => {
    const detail = this.facade.emailTypeDetail();
    return (
      detail?.translations?.map((t) => ({
        code: t.languageCode ?? '',
        name: t.languageName ?? '',
      })) ?? []
    );
  });

  private formInitEffect = effect(() => {
    const detail = this.facade.emailTypeDetail();
    if (detail?.translations) {
      this.initForms(detail.translations);
    }
  });

  ngOnInit(): void {
    const emailTypeParam = this.route.snapshot.paramMap.get('emailType');
    if (emailTypeParam) {
      this.emailType = parseInt(emailTypeParam, 10) as EmailType;
      this.facade.loadEmailTypeDetail(this.emailType);
    }

    this.testEmailForm = this.fb.group({
      recipientEmail: ['', [Validators.required, Validators.email]],
    });

    this.addTranslationForm = this.fb.group({
      key: ['', [Validators.required, Validators.maxLength(100)]],
      value: ['', [Validators.required, Validators.maxLength(5000)]],
    });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private initForms(
    translations: { languageCode?: string; keyValues?: EmailTemplateKeyValueDto[] }[]
  ): void {
    for (const translation of translations) {
      if (!translation.languageCode) continue;

      if (!this.forms.has(translation.languageCode)) {
        const formGroup: Record<string, unknown[]> = {};
        for (const kv of translation.keyValues || []) {
          formGroup[kv.key ?? ''] = [
            kv.value ?? '',
            [Validators.required, Validators.maxLength(5000)],
          ];
        }
        this.forms.set(translation.languageCode, this.fb.group(formGroup));
      }
    }
  }

  getFormForLanguage(languageCode: string): FormGroup | null {
    return this.forms.get(languageCode) ?? null;
  }

  getKeyValuesForLanguage(languageCode: string): EmailTemplateKeyValueDto[] {
    const detail = this.facade.emailTypeDetail();
    const translation = detail?.translations?.find(
      (t) => t.languageCode === languageCode
    );
    return translation?.keyValues ?? [];
  }

  copyVariable(variable: string): void {
    navigator.clipboard.writeText(variable);
    this.snackbarService.showSuccess(
      this.translate.instant('pages.template_management.messages.copied_to_clipboard')
    );
  }

  onSaveTranslation(languageCode: string, templateId: string, key: string): void {
    const form = this.getFormForLanguage(languageCode);
    if (!form || form.get(key)?.invalid) {
      form?.get(key)?.markAsTouched();
      return;
    }

    const value = form.get(key)?.value;
    this.savingKey.set(key);
    this.facade.updateTranslation(templateId, value, () => {
      this.savingKey.set(null);
    });
  }

  openTestEmailDialog(): void {
    this.testEmailForm.reset();
    this.showTestEmailDialog.set(true);
  }

  closeTestEmailDialog(): void {
    this.showTestEmailDialog.set(false);
  }

  onSendTestEmail(): void {
    if (this.testEmailForm.invalid) {
      this.testEmailForm.markAllAsTouched();
      return;
    }

    const detail = this.facade.emailTypeDetail();
    const langCode = this.languages()[0]?.code;

    if (detail?.emailType !== undefined && langCode) {
      this.facade.sendTestEmail(
        detail.emailType,
        langCode,
        this.testEmailForm.value.recipientEmail
      );
      this.closeTestEmailDialog();
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }

  getTranslationForLanguage(languageCode: string): EmailTranslationByLanguageDto | null {
    const detail = this.facade.emailTypeDetail();
    return detail?.translations?.find((t) => t.languageCode === languageCode) ?? null;
  }

  openAddTranslationDialog(languageCode: string): void {
    this.currentLanguageForAdd = this.getTranslationForLanguage(languageCode);
    this.addTranslationForm.reset();
    this.showAddTranslationDialog.set(true);
  }

  closeAddTranslationDialog(): void {
    this.showAddTranslationDialog.set(false);
    this.currentLanguageForAdd = null;
  }

  onAddTranslation(): void {
    if (this.addTranslationForm.invalid) {
      this.addTranslationForm.markAllAsTouched();
      return;
    }

    const detail = this.facade.emailTypeDetail();
    if (!detail || !this.currentLanguageForAdd?.languageId || this.emailType === null) {
      return;
    }

    const { key, value } = this.addTranslationForm.value;
    this.facade.createTranslation(
      this.emailType,
      this.currentLanguageForAdd.languageId,
      key,
      value,
      () => {
        this.closeAddTranslationDialog();
        // Clear forms so they get reinitialized with new data
        this.forms.clear();
      }
    );
  }

  onDeleteTranslation(
    event: Event,
    languageCode: string,
    templateId: string,
    key: string
  ): void {
    this.confirmationService.confirm({
      target: event.target as EventTarget,
      message: this.translate.instant(
        'pages.template_management.dialogs.delete_translation.message',
        { key }
      ),
      header: this.translate.instant(
        'pages.template_management.dialogs.delete_translation.title'
      ),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        if (this.emailType === null) return;
        this.deletingKey.set(key);
        this.facade.deleteTranslation(templateId, this.emailType, () => {
          this.deletingKey.set(null);
          // Clear forms so they get reinitialized with new data
          this.forms.clear();
        });
      },
    });
  }
}
