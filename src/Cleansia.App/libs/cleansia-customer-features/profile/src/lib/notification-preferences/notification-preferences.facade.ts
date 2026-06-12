import { inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  UpdateNotificationPreferencesCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  NotificationPreferenceField,
  NotificationPreferencesValues,
  toPreferencesValues,
} from './notification-preferences.models';

@Injectable()
export class NotificationPreferencesFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly snackbar = inject(SnackbarService);

  readonly preferences = signal<NotificationPreferencesValues | null>(null);
  readonly loading = signal(true);
  readonly hasError = signal(false);
  readonly saving = signal(false);

  load(): void {
    this.loading.set(true);
    this.hasError.set(false);
    this.customerClient.notificationPreferencesClient
      .getMine()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          this.snackbar.showErrorTranslated(
            'pages.profile.notifications.load_error'
          );
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((dto) => {
        if (dto) {
          this.preferences.set(toPreferencesValues(dto));
        }
      });
  }

  setPreference(field: NotificationPreferenceField, value: boolean): void {
    const current = this.preferences();
    if (!current) return;
    this.preferences.set({ ...current, [field]: value });
  }

  save(): void {
    const current = this.preferences();
    if (!current || this.saving()) return;

    this.saving.set(true);
    this.customerClient.notificationPreferencesClient
      .update(new UpdateNotificationPreferencesCommand({ ...current }))
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbar.showErrorTranslated(
            'pages.profile.notifications.save_error'
          );
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((dto) => {
        if (dto) {
          this.preferences.set(toPreferencesValues(dto));
          this.snackbar.showSuccessTranslated(
            'pages.profile.notifications.save_success'
          );
        }
      });
  }
}
