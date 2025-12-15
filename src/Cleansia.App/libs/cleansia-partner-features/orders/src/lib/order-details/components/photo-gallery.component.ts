import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { CleansiaButtonComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

export interface GalleryPhoto {
  id?: string;
  url: string;
  fileName?: string;
  capturedAt?: any;
  capturedByEmployeeName?: string;
  isStaged?: boolean;
}

@Component({
  selector: 'photo-gallery',
  standalone: true,
  imports: [CommonModule, TranslatePipe, CleansiaButtonComponent],
  template: `
    @if (isOpen()) {
    <div class="photo-gallery" (click)="onBackdropClick($event)">
      <div class="photo-gallery__container">
        <!-- Header -->
        <div class="photo-gallery__header">
          <div class="photo-gallery__info">
            <span class="photo-gallery__counter">
              {{ currentIndex() + 1 }} / {{ photos().length }}
            </span>
            @if (currentPhoto().fileName) {
            <span class="photo-gallery__filename">{{
              currentPhoto().fileName
            }}</span>
            } @if (currentPhoto().isStaged) {
            <span class="photo-gallery__staged-badge">{{
              'pages.order_details.staged' | translate
            }}</span>
            }
          </div>
          <button
            type="button"
            class="photo-gallery__close"
            (click)="close()"
            [title]="'global.actions.close' | translate"
          >
            <i class="pi pi-times"></i>
          </button>
        </div>

        <!-- Main Image -->
        <div class="photo-gallery__content">
          <button
            type="button"
            class="photo-gallery__nav photo-gallery__nav--prev"
            [disabled]="!canNavigatePrev()"
            (click)="navigatePrev(); $event.stopPropagation()"
            [title]="'global.actions.previous' | translate"
          >
            <i class="pi pi-chevron-left"></i>
          </button>

          <div class="photo-gallery__image-container">
            <img
              [src]="currentPhoto().url"
              [alt]="currentPhoto().fileName || 'Photo'"
              class="photo-gallery__image"
              (click)="$event.stopPropagation()"
            />
          </div>

          <button
            type="button"
            class="photo-gallery__nav photo-gallery__nav--next"
            [disabled]="!canNavigateNext()"
            (click)="navigateNext(); $event.stopPropagation()"
            [title]="'global.actions.next' | translate"
          >
            <i class="pi pi-chevron-right"></i>
          </button>
        </div>

        <!-- Photo Info & Actions -->
        <div class="photo-gallery__footer">
          <div class="photo-gallery__details">
            @if (currentPhoto().capturedAt) {
            <div class="photo-gallery__detail">
              <i class="pi pi-clock"></i>
              <span>{{ formatDate(currentPhoto().capturedAt) }}</span>
            </div>
            } @if (currentPhoto().capturedByEmployeeName) {
            <div class="photo-gallery__detail">
              <i class="pi pi-user"></i>
              <span>{{ currentPhoto().capturedByEmployeeName }}</span>
            </div>
            }
          </div>

          <div class="photo-gallery__actions">
            @if (canDelete() && !currentPhoto().isStaged) {
            <cleansia-button
              [buttonType]="'button'"
              [style]="'raised-button'"
              [title]="'global.actions.delete' | translate"
              [icon]="'pi pi-trash'"
              [disabled]="deleting()"
              (clickFn)="onDeleteClick(); $event.stopPropagation()"
            />
            } @if (currentPhoto().isStaged) {
            <cleansia-button
              [buttonType]="'button'"
              [style]="'raised-button'"
              [title]="'global.actions.remove' | translate"
              [icon]="'pi pi-times'"
              (clickFn)="onRemoveStagedClick(); $event.stopPropagation()"
            />
            }
          </div>
        </div>

        <!-- Thumbnail Strip -->
        @if (photos().length > 1) {
        <div class="photo-gallery__thumbnails">
          @for (photo of photos(); track $index) {
          <div
            class="photo-gallery__thumbnail"
            [class.photo-gallery__thumbnail--active]="$index === currentIndex()"
            [class.photo-gallery__thumbnail--staged]="photo.isStaged"
            (click)="navigateToIndex($index); $event.stopPropagation()"
          >
            <img [src]="photo.url" [alt]="photo.fileName || 'Thumbnail'" />
            @if (photo.isStaged) {
            <div class="photo-gallery__thumbnail-badge">
              {{ 'pages.order_details.staged' | translate }}
            </div>
            }
          </div>
          }
        </div>
        }
      </div>
    </div>
    }
  `,
})
export class PhotoGalleryComponent {
  readonly photos = input.required<GalleryPhoto[]>();
  readonly initialIndex = input<number>(0);
  readonly canDelete = input<boolean>(true);

  readonly photoDeleted = output<string>();
  readonly stagedPhotoRemoved = output<number>();
  readonly closed = output<void>();

  readonly isOpen = signal(false);
  readonly currentIndex = signal(0);
  readonly deleting = signal(false);

  readonly currentPhoto = computed(() => {
    const photos = this.photos();
    const index = this.currentIndex();
    return photos[index] || { url: '', fileName: '' };
  });

  readonly canNavigatePrev = computed(() => this.currentIndex() > 0);
  readonly canNavigateNext = computed(
    () => this.currentIndex() < this.photos().length - 1
  );

  constructor() {
    effect(() => {
      const index = this.initialIndex();
      this.currentIndex.set(index);
    });

    // Handle keyboard navigation
    effect(() => {
      if (!this.isOpen()) return;

      const handleKeyPress = (event: KeyboardEvent) => {
        switch (event.key) {
          case 'ArrowLeft':
            this.navigatePrev();
            break;
          case 'ArrowRight':
            this.navigateNext();
            break;
          case 'Escape':
            this.close();
            break;
        }
      };

      document.addEventListener('keydown', handleKeyPress);

      return () => {
        document.removeEventListener('keydown', handleKeyPress);
      };
    });
  }

  open(index: number = 0): void {
    this.currentIndex.set(index);
    this.isOpen.set(true);
    document.body.style.overflow = 'hidden';
  }

  close(): void {
    this.isOpen.set(false);
    document.body.style.overflow = '';
    this.closed.emit();
  }

  navigatePrev(): void {
    if (this.canNavigatePrev()) {
      this.currentIndex.update((i) => i - 1);
    }
  }

  navigateNext(): void {
    if (this.canNavigateNext()) {
      this.currentIndex.update((i) => i + 1);
    }
  }

  navigateToIndex(index: number): void {
    this.currentIndex.set(index);
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('photo-gallery')) {
      this.close();
    }
  }

  onDeleteClick(): void {
    const photo = this.currentPhoto();
    if (!photo.id) return;

    this.deleting.set(true);
    this.photoDeleted.emit(photo.id);
  }

  onRemoveStagedClick(): void {
    const index = this.currentIndex();
    this.stagedPhotoRemoved.emit(index);

    // Navigate to next/prev photo or close if this was the last one
    if (this.photos().length === 1) {
      this.close();
    } else if (index === this.photos().length - 1) {
      this.navigatePrev();
    }
  }

  formatDate(date: any): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleString('cs-CZ');
  }
}
