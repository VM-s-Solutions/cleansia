import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  GetOrderPhotosResponse,
  PartnerClient,
  SaveOrderPhotosCommand,
} from '@cleansia/partner-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { finalize, takeUntil, tap } from 'rxjs';
import { StagedPhoto, buildPhotosToSave } from './order-photos.helpers';

@Injectable()
export class OrderPhotosFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly dialogService = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);

  readonly photosData = signal<GetOrderPhotosResponse | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly deleting = signal<string | null>(null);

  loadPhotos(orderId: string): void {
    if (!orderId) return;

    this.loading.set(true);

    this.partnerClient.orderClient
      .getPhotos(orderId)
      .pipe(
        takeUntil(this.destroyed$),
        tap((response) => this.photosData.set(response)),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
  }

  savePhotos(orderId: string, staged: StagedPhoto[], onSuccess: () => void): void {
    if (!orderId || staged.length === 0) return;

    this.saving.set(true);

    const photosToSave = buildPhotosToSave(staged);

    this.partnerClient.orderClient
      .savePhotos(
        new SaveOrderPhotosCommand({
          orderId,
          photos: photosToSave,
        })
      )
      .pipe(
        takeUntil(this.destroyed$),
        tap(() => {
          this.snackbarService.showSuccessTranslated(
            'global.messages.orders.photos_saved'
          );
          onSuccess();
          this.loadPhotos(orderId);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe();
  }

  deletePhoto(orderId: string, photoId: string): void {
    this.dialogService
      .confirmTranslated('pages.order_details.delete_photo_confirm')
      .pipe(takeUntil(this.destroyed$))
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.deleting.set(photoId);

        this.partnerClient.orderClient
          .deletePhoto(photoId)
          .pipe(
            takeUntil(this.destroyed$),
            tap(() => {
              this.snackbarService.showSuccessTranslated(
                'global.messages.orders.photo_deleted'
              );
              this.loadPhotos(orderId);
            }),
            finalize(() => this.deleting.set(null))
          )
          .subscribe();
      });
  }
}
