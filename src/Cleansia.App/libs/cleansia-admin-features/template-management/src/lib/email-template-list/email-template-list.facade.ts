import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, EmailType, EmailTypeListItemDto } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class EmailTemplateListFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly emailTypes = signal<EmailTypeListItemDto[]>([]);
  readonly loading = signal<boolean>(false);

  loadEmailTypes(): void {
    this.loading.set(true);

    this.adminClient.adminEmailTemplateClient
      .types()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([])),
        finalize(() => this.loading.set(false))
      )
      .subscribe((types) => {
        this.emailTypes.set(types || []);
      });
  }

  navigateToDetail(emailType: EmailType): void {
    this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, 'email-templates', emailType, 'translations']);
  }
}
