import { Injectable, inject, signal } from '@angular/core';
import { AdminClient, ChangeOwnPasswordCommand } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  ChangePasswordFormData,
  resolveChangePasswordErrorKey,
} from './admin-profile.models';

@Injectable()
export class AdminProfileFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly saving = signal<boolean>(false);
  readonly passwordChanged = signal<number>(0);

  changePassword(data: ChangePasswordFormData): void {
    if (this.saving()) return;

    this.saving.set(true);
    const command = new ChangeOwnPasswordCommand({
      currentPassword: data.currentPassword,
      newPassword: data.newPassword,
    });

    this.adminClient.adminAuthClient
      .changePassword(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(resolveChangePasswordErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant(
              'pages.admin_profile.messages.change_password_success'
            )
          );
          this.passwordChanged.update((v) => v + 1);
        }
      });
  }
}
