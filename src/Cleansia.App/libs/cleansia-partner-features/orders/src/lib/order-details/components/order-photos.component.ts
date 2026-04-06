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
  CleansiaButtonComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import {
  GetOrderPhotosResponse,
  PartnerClient,
  PhotoType,
  SaveOrderPhotosCommand,
} from '@cleansia/partner-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { finalize, tap } from 'rxjs';
import { PhotoGalleryComponent } from './photo-gallery.component';
import {
  StagedPhoto,
  buildGalleryPhotos,
  buildPhotosToSave,
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
})
export class OrderPhotosComponent {
  private readonly partnerClient = inject(PartnerClient);
  private readonly dialogService = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);

  readonly gallery = viewChild<PhotoGalleryComponent>('gallery');

  readonly orderId = input.required<string>();
  readonly employeeId = input.required<string>();
  readonly canUpload = input<boolean>(true);
  readonly canDelete = input<boolean>(true);

  readonly photosData = signal<GetOrderPhotosResponse | null>(null);
  readonly stagedPhotos = signal<StagedPhoto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal<string | null>(null);

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
        this.loadPhotos();
      }
    });
  }

  loadPhotos(): void {
    const orderId = this.orderId();
    if (!orderId) return;

    this.loading.set(true);

    this.partnerClient.orderClient
      .getPhotos(orderId)
      .pipe(
        tap((response) => this.photosData.set(response)),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
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
    const employeeId = this.employeeId();
    const staged = this.stagedPhotos();

    if (!orderId || !employeeId || staged.length === 0) return;

    this.saving.set(true);

    const photosToSave = buildPhotosToSave(staged);

    this.partnerClient.orderClient
      .savePhotos(
        new SaveOrderPhotosCommand({
          orderId,
          employeeId,
          photos: photosToSave,
        })
      )
      .pipe(
        tap(() => {
          this.snackbarService.showSuccessTranslated(
            'global.messages.orders.photos_saved'
          );
          this.stagedPhotos.set([]);
          this.loadPhotos();
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe();
  }

  deletePhoto(photoId: string): void {
    const employeeId = this.employeeId();
    if (!employeeId) return;

    this.dialogService
      .confirmTranslated('pages.order_details.delete_photo_confirm')
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.deleting.set(photoId);

        this.partnerClient.orderClient
          .deletePhoto(photoId, employeeId)
          .pipe(
            tap(() => {
              this.snackbarService.showSuccessTranslated(
                'global.messages.orders.photo_deleted'
              );
              this.loadPhotos();
            }),
            finalize(() => this.deleting.set(null))
          )
          .subscribe();
      });
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

  formatDate(date: any): string {
    return formatPhotoDate(date);
  }
}
