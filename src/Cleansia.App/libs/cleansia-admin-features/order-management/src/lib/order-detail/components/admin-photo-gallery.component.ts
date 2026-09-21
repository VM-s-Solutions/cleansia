import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { PhotoType } from '@cleansia/admin-services';
import { CleansiaButtonComponent } from '@cleansia/components';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

export interface GalleryPhoto {
  id?: string;
  url: string;
  fileName?: string;
  capturedAt?: Date;
  capturedByEmployeeName?: string;
  photoType?: PhotoType;
}

@Component({
  selector: 'cleansia-admin-photo-gallery',
  standalone: true,
  imports: [CommonModule, TranslatePipe, CleansiaButtonComponent],
  templateUrl: './admin-photo-gallery.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPhotoGalleryComponent {
  private readonly translate = inject(TranslateService);

  readonly photos = input.required<GalleryPhoto[]>();
  readonly initialIndex = input<number>(0);

  readonly closed = output<void>();

  readonly PhotoType = PhotoType;

  readonly isOpen = signal(false);
  readonly currentIndex = signal(0);

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

  open(index = 0): void {
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

  onBackdropClick(event: Event): void {
    if ((event.target as HTMLElement).classList.contains('photo-gallery')) {
      this.close();
    }
  }

  formatDate(date: Date | undefined): string {
    return formatDate(date, this.translate.currentLang, 'dateTime');
  }
}
