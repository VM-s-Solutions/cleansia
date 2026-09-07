import { isPlatformBrowser } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  PLATFORM_ID,
  inject,
  input,
  signal,
} from '@angular/core';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * A legal document — the Terms and the Privacy Policy are the same page with a
 * different namespace, so they are one component with two callers rather than
 * two copies of forty lines that drift.
 *
 * The board's shape is a contents rail beside numbered sections. That rail is
 * the point: these pages are not read, they are SEARCHED — somebody arrives
 * wanting the cancellation rule, and a wall of six unnumbered headings makes
 * them scroll for it. → the "Návratové a právní stránky" board
 */
@Component({
  selector: 'cleansia-customer-legal-document',
  standalone: true,
  imports: [TranslatePipe, FoamEdgeComponent],
  templateUrl: './legal-document.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalDocumentComponent implements AfterViewInit, OnDestroy {
  /** `terms_page` or `privacy_page` — every key on the page hangs off this. */
  readonly namespace = input.required<string>();

  /** Which sections exist. The copy is keyed `section<N>_title` / `_text`. */
  readonly sections = input.required<number[]>();

  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /**
   * The section the reader is currently in, so the rail can say where. Starts
   * on the first one: at the top of the document that IS where they are, and a
   * rail with nothing lit reads as a rail that does not work.
   */
  readonly activeSection = signal<number>(1);

  private frame = 0;
  private readonly onScroll = () => {
    // Coalesced to one read per frame: scroll fires far more often than the
    // rail can usefully change, and every call here measures the document.
    if (this.frame) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      this.syncActiveSection();
    });
  };

  ngAfterViewInit(): void {
    // Server-side there is no viewport to scroll and no document to measure.
    if (!this.isBrowser) return;
    window.addEventListener('scroll', this.onScroll, { passive: true });
    window.addEventListener('resize', this.onScroll, { passive: true });
    this.syncActiveSection();
  }

  ngOnDestroy(): void {
    if (!this.isBrowser) return;
    window.removeEventListener('scroll', this.onScroll);
    window.removeEventListener('resize', this.onScroll);
    if (this.frame) cancelAnimationFrame(this.frame);
  }

  /**
   * The heading the reader has most recently passed.
   *
   * This was an IntersectionObserver over the sections, and it was wrong in a
   * way that only showed up on a long jump: a SECTION is a tall box, so the
   * topmost one intersecting a band near the top of the viewport is whichever
   * started earliest — not the one whose heading is up there. Reading four
   * sections down left the rail lit on two. Asking where each heading is
   * answers the question directly, and it is also right in the case an
   * observer cannot report at all: parked in the middle of one long section,
   * where no heading is crossing anything.
   */
  private syncActiveSection(): void {
    const headings = Array.from(
      this.host.nativeElement.querySelectorAll<HTMLElement>('[data-section]'),
    );
    if (headings.length === 0) return;

    // At the bottom of the document the LAST section is the one being read,
    // whether or not its heading ever reached the line — a short final section
    // cannot be scrolled up there, and the rail would sit one behind forever.
    const atBottom =
      window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
    if (atBottom) {
      const last = Number(headings[headings.length - 1].dataset['section']);
      if (!Number.isNaN(last) && last !== this.activeSection()) this.activeSection.set(last);
      return;
    }

    // The line the heading has to cross to count as read: just under the navbar.
    const line = 96 + 24;
    let current = Number(headings[0].dataset['section']) || 1;
    for (const heading of headings) {
      if (heading.getBoundingClientRect().top > line) break;
      const index = Number(heading.dataset['section']);
      if (!Number.isNaN(index)) current = index;
    }
    if (current !== this.activeSection()) this.activeSection.set(current);
  }

  /** Two digits, as the board sets them: 01, 02 … */
  ordinal(index: number): string {
    return String(index).padStart(2, '0');
  }

  jumpTo(index: number): void {
    if (!this.isBrowser) return;
    this.host.nativeElement
      .querySelector(`[data-section="${index}"]`)
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
