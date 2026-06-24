import { Injectable, inject, signal } from '@angular/core';
import { AdminClient, SendSitewidePromoCommand } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { DialogService, SnackbarService } from '@cleansia/services';
import { EMPTY, catchError, filter, finalize, switchMap, takeUntil, tap } from 'rxjs';

@Injectable()
export class SitewidePushFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly dialog = inject(DialogService);

  readonly submitting = signal(false);

  submit(command: SendSitewidePromoCommand, onSuccess: () => void): void {
    this.dialog
      .confirmTranslated(
        'pages.sitewide_push.confirm_send',
        'pages.sitewide_push.confirm_title'
      )
      .pipe(
        filter((confirmed) => confirmed),
        tap(() => this.submitting.set(true)),
        switchMap(() =>
          this.adminClient.adminMarketingClient.sendSitewidePromo(command).pipe(
            catchError(() => {
              this.snackbar.showErrorTranslated('pages.sitewide_push.send_error');
              return EMPTY;
            }),
            finalize(() => this.submitting.set(false))
          )
        ),
        takeUntil(this.destroyed$)
      )
      .subscribe(() => {
        this.snackbar.showSuccessTranslated('pages.sitewide_push.send_success');
        onSuccess();
      });
  }
}
