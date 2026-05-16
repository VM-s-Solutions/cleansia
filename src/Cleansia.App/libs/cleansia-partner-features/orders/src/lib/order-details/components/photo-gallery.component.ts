import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
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
  capturedAt?: Date | string;
  capturedByEmployeeName?: string;
  isStaged?: boolean;
}

@Component({
  selector: 'photo-gallery',
  standalone: true,
  imports: [CommonModule, TranslatePipe, CleansiaButtonComponent],
  templateUrl: './photo-gallery.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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

  formatDate(date: Date | string | undefined): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleString('en-GB');
  }
}
