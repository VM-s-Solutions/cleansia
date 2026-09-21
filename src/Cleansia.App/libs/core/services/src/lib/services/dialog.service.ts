import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { Observable, Subject } from 'rxjs';

export interface ConfirmOptions {
  /** A destructive act — the accept button turns red so the risk reads before the label does. */
  danger?: boolean;
  icon?: string;
  acceptLabelKey?: string;
  rejectLabelKey?: string;
}

export interface DialogConfig {
  message: string;
  header?: string;
  icon?: string;
  acceptLabel?: string;
  rejectLabel?: string;
  danger?: boolean;
}

/**
 * The one confirmation dialog: title, body, then a text "cancel" and one primary action, rendered by
 * the shell's `<p-confirmDialog>`. Closing the dialog with the X or Escape resolves as a rejection,
 * so every subscriber sees exactly one boolean and completes.
 */
@Injectable({
  providedIn: 'root',
})
export class DialogService {
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  confirm(config: DialogConfig): Observable<boolean> {
    const result$ = new Subject<boolean>();

    this.confirmationService.confirm({
      message: config.message,
      header: config.header || this.translate.instant('global.dialog.confirm'),
      icon: config.icon || 'pi pi-exclamation-triangle',
      acceptLabel: config.acceptLabel || this.translate.instant('global.actions.confirm'),
      rejectLabel: config.rejectLabel || this.translate.instant('global.actions.cancel'),
      acceptButtonProps: { severity: config.danger ? 'danger' : 'primary' },
      rejectButtonProps: { text: true },
      accept: () => {
        result$.next(true);
        result$.complete();
      },
      reject: () => {
        result$.next(false);
        result$.complete();
      },
    });

    return result$.asObservable();
  }

  confirmTranslated(
    messageKey: string,
    headerKey?: string,
    messageParams?: Record<string, unknown>,
    options: ConfirmOptions = {}
  ): Observable<boolean> {
    return this.confirm({
      message: this.translate.instant(messageKey, messageParams),
      header: headerKey ? this.translate.instant(headerKey) : undefined,
      icon: options.icon,
      acceptLabel: options.acceptLabelKey ? this.translate.instant(options.acceptLabelKey) : undefined,
      rejectLabel: options.rejectLabelKey ? this.translate.instant(options.rejectLabelKey) : undefined,
      danger: options.danger,
    });
  }

  confirmDelete(itemName?: string): Observable<boolean> {
    const message = itemName
      ? this.translate.instant('global.dialog.confirm_delete_item', { item: itemName })
      : this.translate.instant('global.dialog.confirm_delete');

    return this.confirm({
      message,
      header: this.translate.instant('global.dialog.delete'),
      icon: 'pi pi-trash',
      acceptLabel: this.translate.instant('global.actions.delete'),
      danger: true,
    });
  }
}
