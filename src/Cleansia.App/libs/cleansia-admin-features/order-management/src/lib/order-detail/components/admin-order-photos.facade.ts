import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  GetOrderPhotosResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { finalize, takeUntil, tap } from 'rxjs';

@Injectable()
export class AdminOrderPhotosFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);

  readonly photosData = signal<GetOrderPhotosResponse | null>(null);
  readonly loading = signal<boolean>(false);

  loadPhotos(orderId: string): void {
    if (!orderId) return;

    this.loading.set(true);

    this.adminClient.adminOrderClient
      .photos(orderId)
      .pipe(
        takeUntil(this.destroyed$),
        tap((response) => this.photosData.set(response)),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        error: (error) => {
          console.error('Error loading photos:', error);
          this.snackbarService.showErrorTranslated('pages.order_detail.messages.photos_error');
        },
      });
  }
}
