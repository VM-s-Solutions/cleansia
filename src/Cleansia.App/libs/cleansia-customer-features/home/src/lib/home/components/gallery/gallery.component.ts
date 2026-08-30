import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  inject,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaTitleComponent,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-gallery',
  templateUrl: './gallery.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CleansiaTitleComponent],
})
export class GalleryComponent {
  private readonly el = inject(ElementRef);
  private readonly cdr = inject(ChangeDetectorRef);

  beforeAfterPairs = [
    { id: 'sofa', before: 'assets/images/gallery/before-sofa.webp', after: 'assets/images/gallery/after-sofa.webp', label: 'pages.home.before_after.label_sofa' },
    { id: 'carpet', before: 'assets/images/gallery/before-carpet.webp', after: 'assets/images/gallery/after-carpet.webp', label: 'pages.home.before_after.label_carpet' },
    { id: 'mattress', before: 'assets/images/gallery/before-mattress.webp', after: 'assets/images/gallery/after-mattress.webp', label: 'pages.home.before_after.label_mattress' },
    { id: 'oven', before: 'assets/images/gallery/before-oven.webp', after: 'assets/images/gallery/after-oven.webp', label: 'pages.home.before_after.label_oven' },
  ];

  private sliderPositions = new Map<string, number>();
  private activeSlider: string | null = null;
  private isDragging = false;

  constructor() {
    this.beforeAfterPairs.forEach(p => this.sliderPositions.set(p.id, 50));
  }

  getSliderPosition(id: string): number {
    return this.sliderPositions.get(id) ?? 50;
  }

  onSliderMouseDown(event: MouseEvent, id: string): void {
    event.preventDefault();
    this.isDragging = true;
    this.activeSlider = id;
    this.updateSliderPosition(event);
  }

  onSliderTouchStart(event: TouchEvent, id: string): void {
    event.preventDefault();
    this.isDragging = true;
    this.activeSlider = id;
    this.updateSliderPositionTouch(event);
  }

  @HostListener('window:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (this.isDragging && this.activeSlider) {
      this.updateSliderPosition(event);
    }
  }

  @HostListener('window:mouseup')
  onMouseUp(): void {
    this.isDragging = false;
    this.activeSlider = null;
  }

  @HostListener('window:touchmove', ['$event'])
  onTouchMove(event: TouchEvent): void {
    if (this.isDragging && this.activeSlider) {
      event.preventDefault();
      this.updateSliderPositionTouch(event);
    }
  }

  @HostListener('window:touchend')
  onTouchEnd(): void {
    this.isDragging = false;
    this.activeSlider = null;
  }

  private updateSliderPosition(event: MouseEvent): void {
    if (!this.activeSlider) return;
    const container = this.el.nativeElement.querySelector(`[data-slider-id="${this.activeSlider}"]`);
    if (!container) return;
    const rect = container.getBoundingClientRect();
    const x = event.clientX - rect.left;
    const pct = Math.max(5, Math.min(95, (x / rect.width) * 100));
    this.sliderPositions.set(this.activeSlider, pct);
    this.cdr.detectChanges();
  }

  private updateSliderPositionTouch(event: TouchEvent): void {
    if (!this.activeSlider || !event.touches[0]) return;
    const container = this.el.nativeElement.querySelector(`[data-slider-id="${this.activeSlider}"]`);
    if (!container) return;
    const rect = container.getBoundingClientRect();
    const x = event.touches[0].clientX - rect.left;
    const pct = Math.max(5, Math.min(95, (x / rect.width) * 100));
    this.sliderPositions.set(this.activeSlider, pct);
    this.cdr.detectChanges();
  }

  /**
   * Responsive sources for a gallery image.
   *
   * The pairs are 1200x900 and the slider renders at roughly 620 CSS px, so a
   * phone was downloading about four times the pixels it could show. The carpet
   * pair alone is 232 KB + 188 KB at full size because of its texture.
   */
  srcsetFor(path: string): string {
    const stem = path.replace('.webp', '');
    return `${stem}-600.webp 600w, ${stem}-900.webp 900w, ${path} 1200w`;
  }
}
