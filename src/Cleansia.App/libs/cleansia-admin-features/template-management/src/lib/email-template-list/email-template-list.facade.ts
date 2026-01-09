import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, EmailType, EmailTypeListItemDto } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class EmailTemplateListFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly emailTypes = signal<EmailTypeListItemDto[]>([]);
  readonly loading = signal<boolean>(false);

  loadEmailTypes(): void {
    this.loading.set(true);

    this.adminClient.adminEmailTemplateClient
      .types()
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.load_error')
          );
          console.error('Error loading email types:', error);
          return of([]);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((types) => {
        this.emailTypes.set(types || []);
      });
  }

  navigateToDetail(emailType: EmailType): void {
    this.router.navigate(['/template-management', 'email-templates', emailType, 'translations']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
