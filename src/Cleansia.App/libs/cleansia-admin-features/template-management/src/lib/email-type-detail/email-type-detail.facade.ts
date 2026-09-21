import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateEmailTemplateTranslationCommand,
  EmailTranslationByLanguageDto,
  EmailType,
  EmailTypeDetailDto,
  SendTestEmailByTypeCommand,
  UpdateEmailTemplateCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class EmailTypeDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly router = inject(Router);

  readonly emailTypeDetail = signal<EmailTypeDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly creating = signal<boolean>(false);
  readonly deleting = signal<boolean>(false);
  readonly deletingKey = signal<string | null>(null);
  readonly sendingTestEmail = signal<boolean>(false);
  readonly selectedLanguageCode = signal<string | null>(null);

  get selectedTranslation(): EmailTranslationByLanguageDto | null {
    const detail = this.emailTypeDetail();
    const langCode = this.selectedLanguageCode();
    if (!detail || !langCode) return null;
    return (
      detail.translations?.find((t) => t.languageCode === langCode) ?? null
    );
  }

  loadEmailTypeDetail(emailType: EmailType): void {
    this.loading.set(true);

    this.adminClient.adminEmailTemplateClient
      .typeDetails(emailType)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((detail) => {
        this.emailTypeDetail.set(detail);
        // Auto-select first language if available
        if (detail?.translations && detail.translations.length > 0) {
          this.selectedLanguageCode.set(
            detail.translations[0].languageCode ?? null
          );
        }
      });
  }

  updateTranslation(
    templateId: string,
    value: string,
    onComplete?: () => void
  ): void {
    this.saving.set(true);

    const command = new UpdateEmailTemplateCommand();
    command.emailTemplateId = templateId;
    command.value = value;

    this.adminClient.adminEmailTemplateClient
      .update(templateId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => {
          this.saving.set(false);
          onComplete?.();
        })
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.template_management.messages.save_success'
          );
        }
      });
  }

  sendTestEmail(
    emailType: EmailType,
    languageCode: string,
    recipientEmail: string
  ): void {
    this.sendingTestEmail.set(true);

    const command = new SendTestEmailByTypeCommand();
    command.emailType = emailType;
    command.languageCode = languageCode;
    command.recipientEmail = recipientEmail;

    this.adminClient.emailTemplateTypesClient
      .sendTest(emailType, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.sendingTestEmail.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.template_management.messages.send_test_success',
            { email: recipientEmail, }
          );
        }
      });
  }

  createTranslation(
    emailType: EmailType,
    languageId: string,
    key: string,
    value: string,
    onComplete?: () => void
  ): void {
    this.creating.set(true);

    const command = new CreateEmailTemplateTranslationCommand();
    command.emailType = emailType;
    command.languageId = languageId;
    command.key = key;
    command.value = value;

    this.adminClient.adminEmailTemplateClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => {
          this.creating.set(false);
          onComplete?.();
        })
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.template_management.messages.create_success'
          );
          // Reload to get updated data
          this.loadEmailTypeDetail(emailType);
        }
      });
  }

  deleteTranslation(
    emailTemplateId: string,
    emailType: EmailType,
    key: string,
    onComplete?: () => void
  ): void {
    this.dialog
      .confirmTranslated(
        'pages.template_management.dialogs.delete_translation.message',
        'pages.template_management.dialogs.delete_translation.title',
        { key },
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deleteTranslationConfirmed(emailTemplateId, emailType, key, onComplete));
  }

  private deleteTranslationConfirmed(
    emailTemplateId: string,
    emailType: EmailType,
    key: string,
    onComplete?: () => void
  ): void {
    this.deleting.set(true);
    this.deletingKey.set(key);

    this.adminClient.adminEmailTemplateClient
      .delete(emailTemplateId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => {
          this.deleting.set(false);
          this.deletingKey.set(null);
          onComplete?.();
        })
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.template_management.messages.delete_success'
          );
          // Reload to get updated data
          this.loadEmailTypeDetail(emailType);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT], {
      fragment: 'email-templates',
    });
  }
}
