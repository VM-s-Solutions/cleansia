import { DatePipe, isPlatformBrowser } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  inject,
  input,
  signal,
} from '@angular/core';
import { CleansiaButtonComponent } from '@cleansia/components';
import { LegalDocumentType } from '@cleansia/customer-services';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { TranslatePipe } from '@ngx-translate/core';
import { Skeleton } from 'primeng/skeleton';
import { LegalDocumentFacade } from './legal-document.facade';

/**
 * A legal document — the customer's legal texts are the same page with a
 * different document type, so they are one component with a caller per type
 * rather than a copy of forty lines per text that drift.
 *
 * The board's shape is a contents rail beside numbered sections. That rail is
 * the point: these pages are not read, they are SEARCHED — somebody arrives
 * wanting the cancellation rule, and a wall of unnumbered headings makes them
 * scroll for it. → the "Návratové a právní stránky" board
 */
@Component({
  selector: 'cleansia-customer-legal-document',
  standalone: true,
  imports: [DatePipe, TranslatePipe, FoamEdgeComponent, CleansiaButtonComponent, Skeleton],
  templateUrl: './legal-document.component.html',
  providers: [LegalDocumentFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalDocumentComponent implements OnInit, AfterViewInit, OnDestroy {
  readonly type = input.required<LegalDocumentType>();

  /** The page's own title, shown until the served document names its own. */
  readonly titleKey = input.required<string>();

  protected readonly facade = inject(LegalDocumentFacade);

  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /**
   * The section the reader is currently in, so the rail can say where. Starts
   * on the first one: at the top of the document that IS where they are, and a
   * rail with nothing lit reads as a rail that does not work.
   */
  readonly activeSection = signal(0);

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

  ngOnInit(): void {
    this.facade.load(this.type());
  }

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
    const headings = this.sectionHeadings();
    if (headings.length === 0) return;

    // At the bottom of the document the LAST section is the one being read,
    // whether or not its heading ever reached the line — a short final section
    // cannot be scrolled up there, and the rail would sit one behind forever.
    const atBottom =
      window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
    if (atBottom) {
      this.activeSection.set(headings.length - 1);
      return;
    }

    // The line the heading has to cross to count as read: just under the navbar.
    const line = 96 + 24;
    let current = 0;
    for (const [index, heading] of headings.entries()) {
      if (heading.getBoundingClientRect().top > line) break;
      current = index;
    }
    if (current !== this.activeSection()) this.activeSection.set(current);
  }

  private sectionHeadings(): HTMLElement[] {
    return Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>('.cl-lgl__content h2'));
  }

  jumpTo(index: number): void {
    if (!this.isBrowser) return;
    this.sectionHeadings()[index]?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
