import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  inject,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-gallery',
  templateUrl: './gallery.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class GalleryComponent {
  private readonly el = inject(ElementRef);
  private readonly cdr = inject(ChangeDetectorRef);

  beforeAfterPairs = [
    { id: 'sofa', before: 'assets/images/gallery/before-sofa.jpg', after: 'assets/images/gallery/after-sofa.jpg', label: 'pages.home.before_after.label_sofa' },
    { id: 'carpet', before: 'assets/images/gallery/before-carpet.jpg', after: 'assets/images/gallery/after-carpet.jpg', label: 'pages.home.before_after.label_carpet' },
    { id: 'mattress', before: 'assets/images/gallery/before-mattress.jpg', after: 'assets/images/gallery/after-mattress.jpg', label: 'pages.home.before_after.label_mattress' },
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
}
