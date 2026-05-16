import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { Observable, Subject } from 'rxjs';

export interface DialogConfig {
  message: string;
  header?: string;
  icon?: string;
  acceptLabel?: string;
  rejectLabel?: string;
  acceptButtonStyleClass?: string;
  rejectButtonStyleClass?: string;
}

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
      acceptButtonStyleClass: config.acceptButtonStyleClass || 'p-button-danger',
      rejectButtonStyleClass: config.rejectButtonStyleClass || 'p-button-text',
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
    messageParams?: Record<string, unknown>
  ): Observable<boolean> {
    return this.confirm({
      message: this.translate.instant(messageKey, messageParams),
      header: headerKey ? this.translate.instant(headerKey) : undefined,
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
      rejectLabel: this.translate.instant('global.actions.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
    });
  }
}
