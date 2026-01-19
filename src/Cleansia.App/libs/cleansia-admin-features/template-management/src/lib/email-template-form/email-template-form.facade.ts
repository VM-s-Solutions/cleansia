import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  EmailTemplateTranslationDetailDto,
  SendTestEmailCommand,
  UpdateEmailTemplateCommand,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface EmailTemplateFormData {
  value: string;
}

@Injectable()
export class EmailTemplateFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly template = signal<EmailTemplateTranslationDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly sendingTestEmail = signal<boolean>(false);

  loadTemplate(templateId: string): void {
    this.loading.set(true);

    this.adminClient.adminEmailTemplateClient
      .details(templateId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((template) => {
        this.template.set(template);
      });
  }

  updateTemplate(templateId: string, data: EmailTemplateFormData): void {
    this.saving.set(true);

    const command = new UpdateEmailTemplateCommand({
      emailTemplateId: templateId,
      value: data.value,
    });

    this.adminClient.adminEmailTemplateClient
      .update(templateId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.save_success')
          );
          this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, 'email-templates']);
        }
      });
  }

  sendTestEmail(templateId: string, recipientEmail: string): void {
    this.sendingTestEmail.set(true);

    const command = new SendTestEmailCommand({
      emailTemplateId: templateId,
      recipientEmail: recipientEmail,
    });

    this.adminClient.adminEmailTemplateClient
      .sendTest(templateId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.sendingTestEmail.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.send_test_success', {
              email: recipientEmail,
            })
          );
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, 'email-templates']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
