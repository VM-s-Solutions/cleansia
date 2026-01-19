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
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class EmailTypeDetailFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly emailTypeDetail = signal<EmailTypeDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly creating = signal<boolean>(false);
  readonly deleting = signal<boolean>(false);
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
        takeUntil(this.destroy$),
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

  selectLanguage(languageCode: string): void {
    this.selectedLanguageCode.set(languageCode);
  }

  updateTranslation(
    templateId: string,
    value: string,
    onComplete?: () => void
  ): void {
    this.saving.set(true);

    const command = new UpdateEmailTemplateCommand({
      emailTemplateId: templateId,
      value: value,
    });

    this.adminClient.adminEmailTemplateClient
      .update(templateId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => {
          this.saving.set(false);
          onComplete?.();
        })
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.template_management.messages.save_success'
            )
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

    const command = new SendTestEmailByTypeCommand({
      emailType: emailType,
      languageCode: languageCode,
      recipientEmail: recipientEmail,
    });

    this.adminClient.emailTemplateTypesClient
      .sendTest(emailType, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.sendingTestEmail.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.template_management.messages.send_test_success',
              {
                email: recipientEmail,
              }
            )
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

    const command = new CreateEmailTemplateTranslationCommand({
      emailType: emailType,
      languageId: languageId,
      key: key,
      value: value,
    });

    this.adminClient.adminEmailTemplateClient
      .create(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => {
          this.creating.set(false);
          onComplete?.();
        })
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.template_management.messages.create_success'
            )
          );
          // Reload to get updated data
          this.loadEmailTypeDetail(emailType);
        }
      });
  }

  deleteTranslation(
    emailTemplateId: string,
    emailType: EmailType,
    onComplete?: () => void
  ): void {
    this.deleting.set(true);

    this.adminClient.adminEmailTemplateClient
      .delete(emailTemplateId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => {
          this.deleting.set(false);
          onComplete?.();
        })
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.template_management.messages.delete_success'
            )
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
