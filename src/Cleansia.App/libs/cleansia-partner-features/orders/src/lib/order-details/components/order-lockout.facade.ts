import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { PartnerClient, PhotoType, ReportOrderLockoutCommand } from '@cleansia/partner-services';
import { extractApiErrorCode, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import {
  catchError,
  defer,
  finalize,
  fromEvent,
  map,
  merge,
  of,
  repeat,
  Subscription,
  takeUntil,
  takeWhile,
  tap,
  timer,
} from 'rxjs';
import { OrderPhotosFacade } from './order-photos.facade';
import { createStagedPhoto, filterPhotosByType } from './order-photos.helpers';

// A browser timer runs on the monotonic clock, which stops while the phone sleeps, so the wait is walked in short steps.
const CLOCK_STEP_MS = 30_000;

@Injectable()
export class OrderLockoutFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly photos = inject(OrderPhotosFacade);
  private readonly document = inject(DOCUMENT);

  readonly lang = currentLanguage(inject(TranslateService));
  readonly now = signal(Date.now());
  readonly reporting = signal(false);
  readonly uploading = this.photos.saving;
  readonly entrancePhotos = computed(() =>
    filterPhotosByType(this.photos.photosData(), PhotoType.Entrance)
  );

  private wake: Subscription | null = null;

  loadEntrancePhotos(orderId: string): void {
    this.photos.loadPhotos(orderId);
  }

  uploadEntrancePhoto(orderId: string, base64Content: string, file: File): void {
    this.photos.savePhotos(
      orderId,
      [createStagedPhoto(base64Content, file, PhotoType.Entrance)],
      () => undefined
    );
  }

  report(orderId: string, callAttempts: string, onSettled: () => void): void {
    const note = callAttempts.trim();
    if (!orderId || !note || this.reporting()) {
      return;
    }

    this.reporting.set(true);

    const command = new ReportOrderLockoutCommand();
    command.orderId = orderId;
    command.callAttempts = note;

    this.partnerClient.orderClient
      .reportLockout(command)
      .pipe(
        takeUntil(this.destroyed$),
        tap(() => {
          this.snackbarService.showSuccessTranslated('global.messages.orders.lockout_reported');
          onSettled();
        }),
        catchError((error) => {
          if (extractApiErrorCode(error)) {
            onSettled();
          }
          return of(null);
        }),
        finalize(() => this.reporting.set(false))
      )
      .subscribe();
  }

  wakeAt(moment: Date): void {
    this.wake?.unsubscribe();
    const opensAt = moment.getTime();
    const step$ = defer(() => timer(Math.min(Math.max(opensAt - Date.now(), 0), CLOCK_STEP_MS))).pipe(repeat());
    this.wake = merge(step$, fromEvent(this.document, 'visibilitychange'))
      .pipe(
        map(() => Date.now()),
        takeWhile((now) => now < opensAt, true),
        takeUntil(this.destroyed$)
      )
      .subscribe((now) => this.now.set(now));
  }
}
