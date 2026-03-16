import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  inject,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'cleansia-scroll-top',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      class="scroll-top-btn"
      [class.visible]="visible()"
      (click)="scrollToTop()"
      aria-label="Scroll to top"
    >
      <i class="pi pi-arrow-up"></i>
    </button>
  `,
  styles: `
    .scroll-top-btn {
      position: fixed;
      bottom: 2rem;
      right: 2rem;
      width: 44px;
      height: 44px;
      border-radius: 50%;
      border: none;
      background: #0284c7;
      color: #fff;
      font-size: 1.1rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      opacity: 0;
      visibility: hidden;
      transform: translateY(12px);
      transition:
        opacity 0.3s ease,
        visibility 0.3s ease,
        transform 0.3s ease,
        background 0.2s ease;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.18);
      z-index: 1000;

      &.visible {
        opacity: 1;
        visibility: visible;
        transform: translateY(0);
      }

      &:hover {
        background: #0369a1;
        transform: translateY(-2px);
        box-shadow: 0 6px 20px rgba(0, 0, 0, 0.25);
      }

      &:active {
        transform: translateY(0);
      }
    }
  `,
})
export class CleansiaScrollTopComponent {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly visible = signal(false);

  @HostListener('window:scroll')
  onScroll(): void {
    if (!this.isBrowser) return;
    this.visible.set(window.scrollY > 300);
  }

  scrollToTop(): void {
    if (!this.isBrowser) return;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
