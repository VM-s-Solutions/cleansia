import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import {
  AdminClient,
  GetOrderPhotosResponse,
  PhotoType,
} from '@cleansia/admin-services';
import {
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { finalize, tap } from 'rxjs';
import {
  AdminPhotoGalleryComponent,
  GalleryPhoto,
} from './admin-photo-gallery.component';

@Component({
  selector: 'admin-order-photos',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    AdminPhotoGalleryComponent,
  ],
  templateUrl: './admin-order-photos.component.html',
})
export class AdminOrderPhotosComponent {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly gallery = viewChild<AdminPhotoGalleryComponent>('gallery');

  readonly orderId = input.required<string>();

  readonly photosData = signal<GetOrderPhotosResponse | null>(null);
  readonly loading = signal(false);

  readonly beforePhotos = computed(() => {
    return (
      this.photosData()?.photos?.filter(
        (p) => p.photoType === PhotoType.Before
      ) || []
    );
  });

  readonly afterPhotos = computed(() => {
    return (
      this.photosData()?.photos?.filter(
        (p) => p.photoType === PhotoType.After
      ) || []
    );
  });

  readonly galleryPhotos = computed<GalleryPhoto[]>(() => {
    const photos = this.photosData()?.photos || [];
    return photos.map((p) => ({
      id: p.id,
      url: p.blobUrl!,
      fileName: p.originalFileName || p.fileName,
      capturedAt: p.capturedAt,
      capturedByEmployeeName: p.capturedByEmployeeName,
      photoType: p.photoType,
    }));
  });

  constructor() {
    effect(() => {
      const orderId = this.orderId();
      if (orderId) {
        this.loadPhotos();
      }
    });
  }

  loadPhotos(): void {
    const orderId = this.orderId();
    if (!orderId) return;

    this.loading.set(true);

    this.adminClient.adminOrderClient
      .photos(orderId)
      .pipe(
        tap((response) => this.photosData.set(response)),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        error: (error) => {
          console.error('Error loading photos:', error);
          this.snackbarService.showError(
            this.translate.instant('pages.order_detail.messages.photos_error')
          );
        },
      });
  }

  openGallery(index: number): void {
    this.gallery()?.open(index);
  }

  formatDate(date: Date | null | undefined): string {
    if (!date) return '';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }
}
