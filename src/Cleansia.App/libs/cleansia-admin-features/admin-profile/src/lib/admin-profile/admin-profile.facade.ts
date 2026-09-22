import { Injectable, inject, signal } from '@angular/core';
import { AdminClient, ChangeOwnPasswordCommand } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { ChangePasswordFormData } from './admin-profile.models';

@Injectable()
export class AdminProfileFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);

  readonly saving = signal<boolean>(false);
  readonly passwordChanged = signal<number>(0);

  changePassword(data: ChangePasswordFormData): void {
    if (this.saving()) return;

    this.saving.set(true);
    const command = new ChangeOwnPasswordCommand();
    command.currentPassword = data.currentPassword;
    command.newPassword = data.newPassword;
    // Server-enriched from the HttpOnly refresh cookie so the change spares the caller's own
    // session; the value sent here is ignored. Empty satisfies the generated required field.
    command.currentRefreshToken = '';

    this.adminClient.adminAuthClient
      .changePassword(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(
            'pages.admin_profile.messages.change_password_success'
          );
          this.passwordChanged.update((v) => v + 1);
        }
      });
  }
}
