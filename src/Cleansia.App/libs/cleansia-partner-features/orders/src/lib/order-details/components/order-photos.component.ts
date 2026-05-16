import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { PhotoType } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { OrderPhotosFacade } from './order-photos.facade';
import { PhotoGalleryComponent } from './photo-gallery.component';
import {
  StagedPhoto,
  buildGalleryPhotos,
  calculateStagedIndex,
  createStagedPhoto,
  filterPhotosByType,
  filterStagedByType,
  formatPhotoDate,
  validatePhotoFile,
} from './order-photos.helpers';

@Component({
  selector: 'order-photos',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    PhotoGalleryComponent,
  ],
  templateUrl: './order-photos.component.html',
  styleUrls: ['./order-photos.component.scss'],
  providers: [OrderPhotosFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderPhotosComponent {
  protected readonly facade = inject(OrderPhotosFacade);
  private readonly snackbarService = inject(SnackbarService);

  readonly gallery = viewChild<PhotoGalleryComponent>('gallery');

  readonly orderId = input.required<string>();
  readonly employeeId = input.required<string>();
  readonly canUpload = input<boolean>(true);
  readonly canDelete = input<boolean>(true);

  readonly stagedPhotos = signal<StagedPhoto[]>([]);

  readonly photosData = this.facade.photosData;
  readonly loading = this.facade.loading;
  readonly saving = this.facade.saving;
  readonly deleting = this.facade.deleting;

  readonly beforePhotos = computed(() =>
    filterPhotosByType(this.photosData(), PhotoType.Before)
  );

  readonly afterPhotos = computed(() =>
    filterPhotosByType(this.photosData(), PhotoType.After)
  );

  readonly stagedBeforePhotos = computed(() =>
    filterStagedByType(this.stagedPhotos(), PhotoType.Before)
  );

  readonly stagedAfterPhotos = computed(() =>
    filterStagedByType(this.stagedPhotos(), PhotoType.After)
  );

  readonly hasStagedPhotos = computed(() => this.stagedPhotos().length > 0);

  readonly galleryPhotos = computed(() =>
    buildGalleryPhotos(this.photosData(), this.stagedPhotos())
  );

  constructor() {
    effect(() => {
      const orderId = this.orderId();
      if (orderId) {
        this.facade.loadPhotos(orderId);
      }
    });
  }

  onFilesSelected(event: Event, photoType: PhotoType): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files || []);

    if (files.length === 0) return;

    for (const file of files) {
      const validation = validatePhotoFile(file);
      if (!validation.valid) {
        this.snackbarService.showErrorTranslated(validation.errorKey!);
        continue;
      }
      this.stagePhoto(file, photoType);
    }

    input.value = '';
  }

  stagePhoto(file: File, photoType: PhotoType): void {
    const reader = new FileReader();

    reader.onload = () => {
      const base64String = reader.result as string;
      const stagedPhoto = createStagedPhoto(base64String, file, photoType);
      this.stagedPhotos.update((photos) => [...photos, stagedPhoto]);
    };

    reader.onerror = () => {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.photo_read_failed'
      );
    };

    reader.readAsDataURL(file);
  }

  removeStagedPhoto(index: number): void {
    this.stagedPhotos.update((photos) => photos.filter((_, i) => i !== index));
  }

  clearStagedPhotos(): void {
    this.stagedPhotos.set([]);
  }

  savePhotos(): void {
    const orderId = this.orderId();
    const staged = this.stagedPhotos();
    this.facade.savePhotos(orderId, staged, () => this.stagedPhotos.set([]));
  }

  deletePhoto(photoId: string): void {
    this.facade.deletePhoto(this.orderId(), photoId);
  }

  openGallery(index: number): void {
    this.gallery()?.open(index);
  }

  onGalleryPhotoDeleted(photoId: string): void {
    this.deletePhoto(photoId);
  }

  onGalleryStagedPhotoRemoved(globalIndex: number): void {
    const uploadedCount = this.photosData()?.photos?.length || 0;
    const stagedIndex = calculateStagedIndex(globalIndex, uploadedCount);

    if (stagedIndex >= 0) {
      this.removeStagedPhoto(stagedIndex);
    }
  }

  viewPhoto(url: string): void {
    window.open(url, '_blank');
  }

  formatDate(date: Date | string | undefined): string {
    return formatPhotoDate(date);
  }
}
