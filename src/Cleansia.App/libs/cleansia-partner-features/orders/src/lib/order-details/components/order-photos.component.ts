import { Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CleansiaButtonComponent, CleansiaSectionComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Client,
  DialogService,
  SnackbarService,
  PhotoType,
  GetOrderPhotosResponse,
  BlobFileDto,
  SaveOrderPhotosCommand,
  SaveOrderPhotosPhotoToSave,
} from '@cleansia/services';
import { finalize, tap } from 'rxjs';
import { PhotoGalleryComponent, GalleryPhoto } from './photo-gallery.component';

interface StagedPhoto {
  file: BlobFileDto;
  photoType: PhotoType;
  notes?: string;
  preview: string;
}

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
  template: `
    <cleansia-section [title]="'pages.order_details.photos' | translate">
      <!-- Upload Section -->
      <div class="order-photos__upload">
        <div class="order-photos__upload-buttons">
          <input
            #beforeFileInput
            type="file"
            accept="image/jpeg,image/jpg,image/png,image/webp"
            multiple
            (change)="onFilesSelected($event, 1)"
            style="display: none"
          />
          <cleansia-button
            [buttonType]="'button'"
            [style]="'raised-button'"
            [title]="'pages.order_details.add_before_photos' | translate"
            [icon]="'pi pi-camera'"
            [disabled]="saving() || !canUpload()"
            (clickFn)="beforeFileInput.click()"
          />

          <input
            #afterFileInput
            type="file"
            accept="image/jpeg,image/jpg,image/png,image/webp"
            multiple
            (change)="onFilesSelected($event, 2)"
            style="display: none"
          />
          <cleansia-button
            [buttonType]="'button'"
            [style]="'raised-button'"
            [title]="'pages.order_details.add_after_photos' | translate"
            [icon]="'pi pi-camera'"
            [disabled]="saving() || !canUpload()"
            (clickFn)="afterFileInput.click()"
          />
        </div>

        @if (hasStagedPhotos()) {
          <div class="order-photos__save-section">
            <p class="order-photos__staged-count">
              {{ 'pages.order_details.staged_photos_count' | translate: {count: stagedPhotos().length} }}
            </p>
            <div class="order-photos__save-buttons">
              <cleansia-button
                [buttonType]="'button'"
                [style]="'raised-button'"
                [title]="'pages.order_details.cancel_staged' | translate"
                [icon]="'pi pi-times'"
                [disabled]="saving()"
                (clickFn)="clearStagedPhotos()"
              />
              <cleansia-button
                [buttonType]="'button'"
                [style]="'raised-button'"
                [title]="'pages.order_details.save_photos' | translate"
                [icon]="'pi pi-save'"
                [disabled]="saving()"
                (clickFn)="savePhotos()"
              />
            </div>
          </div>
        }

        @if (saving()) {
          <div class="order-photos__upload-progress">
            <i class="pi pi-spin pi-spinner"></i>
            <span>{{ 'pages.order_details.saving_photos' | translate }}</span>
          </div>
        }
      </div>

      <!-- Photo Counts -->
      @if (photosData() || hasStagedPhotos()) {
        <div class="order-photos__counts">
          <div class="order-photos__count">
            <span class="order-photos__count-label">{{ 'pages.order_details.before_photos' | translate }}:</span>
            <span class="order-photos__count-value">
              {{ beforePhotos().length + stagedBeforePhotos().length }}
              @if (stagedBeforePhotos().length > 0) {
                <span class="order-photos__staged-indicator">(+{{ stagedBeforePhotos().length }} staged)</span>
              }
            </span>
          </div>
          <div class="order-photos__count">
            <span class="order-photos__count-label">{{ 'pages.order_details.after_photos' | translate }}:</span>
            <span class="order-photos__count-value">
              {{ afterPhotos().length + stagedAfterPhotos().length }}
              @if (stagedAfterPhotos().length > 0) {
                <span class="order-photos__staged-indicator">(+{{ stagedAfterPhotos().length }} staged)</span>
              }
            </span>
          </div>
        </div>
      }

      <!-- Photo Gallery -->
      <div class="order-photos__gallery">
        <!-- Before Photos -->
        @if (beforePhotos().length > 0 || stagedBeforePhotos().length > 0) {
          <div class="order-photos__section">
            <h4 class="order-photos__section-title">{{ 'pages.order_details.before_photos' | translate }}</h4>

            <!-- Uploaded Photos -->
            <div class="order-photos__grid">
              @for (photo of beforePhotos(); track photo.id) {
                <div class="order-photos__item">
                  <img
                    [src]="photo.blobUrl"
                    [alt]="photo.originalFileName || photo.fileName"
                    class="order-photos__image"
                    (click)="openGallery($index)"
                  />
                  <div class="order-photos__item-info">
                    <span class="order-photos__item-name">{{ photo.originalFileName || photo.fileName }}</span>
                    <span class="order-photos__item-date">{{ formatDate(photo.capturedAt) }}</span>
                    @if (photo.capturedByEmployeeName) {
                      <span class="order-photos__item-employee">{{ photo.capturedByEmployeeName }}</span>
                    }
                  </div>
                  @if (canDelete()) {
                    <button
                      type="button"
                      class="order-photos__delete"
                      [disabled]="deleting() === photo.id"
                      (click)="deletePhoto(photo.id!); $event.stopPropagation()"
                      [title]="'global.actions.delete' | translate"
                    >
                      @if (deleting() === photo.id) {
                        <i class="pi pi-spin pi-spinner"></i>
                      } @else {
                        <i class="pi pi-trash"></i>
                      }
                    </button>
                  }
                </div>
              }

              <!-- Staged Before Photos -->
              @for (staged of stagedBeforePhotos(); track $index) {
                <div class="order-photos__item order-photos__item--staged">
                  <div class="order-photos__staged-badge">{{ 'pages.order_details.staged' | translate }}</div>
                  <img
                    [src]="staged.preview"
                    [alt]="staged.file.fileName"
                    class="order-photos__image"
                    (click)="openGallery(beforePhotos().length + $index)"
                  />
                  <div class="order-photos__item-info">
                    <span class="order-photos__item-name">{{ staged.file.fileName }}</span>
                    <span class="order-photos__item-status">{{ 'pages.order_details.pending_upload' | translate }}</span>
                  </div>
                  <button
                    type="button"
                    class="order-photos__delete"
                    (click)="removeStagedPhoto($index); $event.stopPropagation()"
                    [title]="'global.actions.remove' | translate"
                  >
                    <i class="pi pi-times"></i>
                  </button>
                </div>
              }
            </div>
          </div>
        }

        <!-- After Photos -->
        @if (afterPhotos().length > 0 || stagedAfterPhotos().length > 0) {
          <div class="order-photos__section">
            <h4 class="order-photos__section-title">{{ 'pages.order_details.after_photos' | translate }}</h4>

            <!-- Uploaded Photos -->
            <div class="order-photos__grid">
              @for (photo of afterPhotos(); track photo.id) {
                <div class="order-photos__item">
                  <img
                    [src]="photo.blobUrl"
                    [alt]="photo.originalFileName || photo.fileName"
                    class="order-photos__image"
                    (click)="openGallery(beforePhotos().length + stagedBeforePhotos().length + $index)"
                  />
                  <div class="order-photos__item-info">
                    <span class="order-photos__item-name">{{ photo.originalFileName || photo.fileName }}</span>
                    <span class="order-photos__item-date">{{ formatDate(photo.capturedAt) }}</span>
                    @if (photo.capturedByEmployeeName) {
                      <span class="order-photos__item-employee">{{ photo.capturedByEmployeeName }}</span>
                    }
                  </div>
                  @if (canDelete()) {
                    <button
                      type="button"
                      class="order-photos__delete"
                      [disabled]="deleting() === photo.id"
                      (click)="deletePhoto(photo.id!); $event.stopPropagation()"
                      [title]="'global.actions.delete' | translate"
                    >
                      @if (deleting() === photo.id) {
                        <i class="pi pi-spin pi-spinner"></i>
                      } @else {
                        <i class="pi pi-trash"></i>
                      }
                    </button>
                  }
                </div>
              }

              <!-- Staged After Photos -->
              @for (staged of stagedAfterPhotos(); track $index) {
                <div class="order-photos__item order-photos__item--staged">
                  <div class="order-photos__staged-badge">{{ 'pages.order_details.staged' | translate }}</div>
                  <img
                    [src]="staged.preview"
                    [alt]="staged.file.fileName"
                    class="order-photos__image"
                    (click)="openGallery(beforePhotos().length + stagedBeforePhotos().length + afterPhotos().length + $index)"
                  />
                  <div class="order-photos__item-info">
                    <span class="order-photos__item-name">{{ staged.file.fileName }}</span>
                    <span class="order-photos__item-status">{{ 'pages.order_details.pending_upload' | translate }}</span>
                  </div>
                  <button
                    type="button"
                    class="order-photos__delete"
                    (click)="removeStagedPhoto(beforePhotos().length + stagedBeforePhotos().length + afterPhotos().length + $index); $event.stopPropagation()"
                    [title]="'global.actions.remove' | translate"
                  >
                    <i class="pi pi-times"></i>
                  </button>
                </div>
              }
            </div>
          </div>
        }

        @if (beforePhotos().length === 0 && afterPhotos().length === 0 && !hasStagedPhotos() && !loading()) {
          <div class="order-photos__empty">
            <i class="pi pi-images"></i>
            <p>{{ 'pages.order_details.no_photos' | translate }}</p>
          </div>
        }
      </div>

      @if (loading()) {
        <div class="order-photos__loader">
          <i class="pi pi-spin pi-spinner"></i>
          <span>{{ 'global.messages.loading' | translate }}</span>
        </div>
      }
    </cleansia-section>

    <!-- Photo Gallery -->
    <photo-gallery
      #gallery
      [photos]="galleryPhotos()"
      [canDelete]="canDelete()"
      (photoDeleted)="onGalleryPhotoDeleted($event)"
      (stagedPhotoRemoved)="onGalleryStagedPhotoRemoved($event)"
    />
  `,
  styles: [`
    .order-photos__save-section {
      margin-top: 1rem;
      padding: 1rem;
      background-color: #f8f9fa;
      border-radius: 8px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .order-photos__staged-count {
      margin: 0;
      font-weight: 500;
      color: #2c3e50;
    }

    .order-photos__save-buttons {
      display: flex;
      gap: 0.5rem;
    }

    .order-photos__item--staged {
      position: relative;
      border: 2px solid #ffc107;
    }

    .order-photos__staged-badge {
      position: absolute;
      top: 8px;
      left: 8px;
      background-color: #ffc107;
      color: white;
      padding: 4px 8px;
      border-radius: 4px;
      font-size: 12px;
      font-weight: 600;
      z-index: 1;
      text-transform: uppercase;
    }

    .order-photos__item-status {
      font-size: 12px;
      color: #ffc107;
      font-style: italic;
    }

    .order-photos__staged-indicator {
      color: #ffc107;
      font-size: 0.9em;
      margin-left: 4px;
    }
  `]
})
export class OrderPhotosComponent {
  private readonly client = inject(Client);
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

  readonly beforePhotos = computed(() => {
    return this.photosData()?.photos?.filter((p) => p.photoType === PhotoType.Before) || [];
  });

  readonly afterPhotos = computed(() => {
    return this.photosData()?.photos?.filter((p) => p.photoType === PhotoType.After) || [];
  });

  readonly stagedBeforePhotos = computed(() => {
    return this.stagedPhotos().filter((p) => p.photoType === PhotoType.Before);
  });

  readonly stagedAfterPhotos = computed(() => {
    return this.stagedPhotos().filter((p) => p.photoType === PhotoType.After);
  });

  readonly hasStagedPhotos = computed(() => {
    return this.stagedPhotos().length > 0;
  });

  readonly galleryPhotos = computed<GalleryPhoto[]>(() => {
    const uploaded = this.photosData()?.photos?.map(p => ({
      id: p.id,
      url: p.blobUrl!,
      fileName: p.originalFileName || p.fileName,
      capturedAt: p.capturedAt,
      capturedByEmployeeName: p.capturedByEmployeeName,
      isStaged: false
    })) || [];

    const staged = this.stagedPhotos().map(s => ({
      url: s.preview,
      fileName: s.file.fileName,
      isStaged: true
    }));

    return [...uploaded, ...staged];
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

    this.client.orderClient.getPhotos(orderId)
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

    const maxSize = 10 * 1024 * 1024;
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];

    for (const file of files) {
      if (file.size > maxSize) {
        this.snackbarService.showErrorTranslated('global.messages.orders.photo_size_exceeded');
        continue;
      }

      if (!allowedTypes.includes(file.type)) {
        this.snackbarService.showErrorTranslated('global.messages.orders.photo_invalid_type');
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

      const stagedPhoto: StagedPhoto = {
        file: new BlobFileDto({
          fileName: file.name,
          base64Content: base64String,
          contentType: file.type
        }),
        photoType,
        preview: base64String
      };

      this.stagedPhotos.update(photos => [...photos, stagedPhoto]);
    };

    reader.onerror = () => {
      this.snackbarService.showErrorTranslated('global.messages.orders.photo_read_failed');
    };

    reader.readAsDataURL(file);
  }

  removeStagedPhoto(index: number): void {
    this.stagedPhotos.update(photos => photos.filter((_, i) => i !== index));
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

    const photosToSave = staged.map(sp => new SaveOrderPhotosPhotoToSave({
      photoType: sp.photoType,
      file: sp.file,
      notes: sp.notes
    }));

    this.client.orderClient.savePhotos(new SaveOrderPhotosCommand({
      orderId,
      employeeId,
      photos: photosToSave
    }))
      .pipe(
        tap(() => {
          this.snackbarService.showSuccessTranslated('global.messages.orders.photos_saved');
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

    this.dialogService.confirmTranslated('pages.order_details.delete_photo_confirm')
      .subscribe(confirmed => {
        if (!confirmed) return;

        this.deleting.set(photoId);

        this.client.orderClient.deletePhoto(photoId, employeeId)
          .pipe(
            tap(() => {
              this.snackbarService.showSuccessTranslated('global.messages.orders.photo_deleted');
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
    // Calculate the local index within staged photos
    const uploadedCount = (this.photosData()?.photos?.length || 0);
    const stagedIndex = globalIndex - uploadedCount;

    if (stagedIndex >= 0) {
      this.removeStagedPhoto(stagedIndex);
    }
  }

  viewPhoto(url: string): void {
    window.open(url, '_blank');
  }

  formatDate(date: any): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleString('cs-CZ');
  }
}
