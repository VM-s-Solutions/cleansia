import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { PhotoType } from '@cleansia/admin-services';
import {
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  AdminPhotoGalleryComponent,
  GalleryPhoto,
} from './admin-photo-gallery.component';
import { AdminOrderPhotosFacade } from './admin-order-photos.facade';

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
  providers: [AdminOrderPhotosFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOrderPhotosComponent {
  protected readonly facade = inject(AdminOrderPhotosFacade);
  private readonly translate = inject(TranslateService);

  readonly gallery = viewChild<AdminPhotoGalleryComponent>('gallery');

  readonly orderId = input.required<string>();

  readonly photosData = this.facade.photosData;
  readonly loading = this.facade.loading;

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
        this.facade.loadPhotos(orderId);
      }
    });
  }

  openGallery(index: number): void {
    this.gallery()?.open(index);
  }

  formatDate(date: Date | null | undefined): string {
    if (!date) return '';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString(this.translate.currentLang || 'en-GB');
  }
}
