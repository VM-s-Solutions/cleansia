import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { CleansiaButtonComponent } from '../cleansia-button';
import { FilterDrawerState } from './filter-drawer-state';

/**
 * The one filter drawer: the `Filtry` trigger with its active count, and the side panel it opens —
 * a modal dialog with a heading, the projected filter fields and a Reset footer. Sits in the page
 * header's action row; the chips it produces render through `cleansia-filter-chips`.
 *
 * @example
 * <cleansia-filter-drawer [state]="facade.filters">
 *   <form [formGroup]="facade.filterForm" class="filter-form">…</form>
 * </cleansia-filter-drawer>
 */
@Component({
  selector: 'cleansia-filter-drawer',
  standalone: true,
  imports: [TranslatePipe, CleansiaButtonComponent],
  templateUrl: './cleansia-filter-drawer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaFilterDrawerComponent {
  state = input.required<FilterDrawerState>();
  /** The dialog's name; the shared `global.filters.title` when the page has no better one. */
  title = input<string>('');

  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  private readonly trigger = viewChild.required<ElementRef<HTMLElement>>('trigger');
  private readonly close = viewChild.required('close', { read: ElementRef<HTMLElement> });
  private readonly reset = viewChild.required('reset', { read: ElementRef<HTMLElement> });
  private wasOpen = false;

  constructor() {
    // After render, not during it: the panel is inert until the open class lands, and an inert
    // element refuses focus.
    afterRenderEffect(() => {
      const open = this.state().isOpen();
      if (open) {
        this.panel().nativeElement.focus();
      } else if (this.wasOpen) {
        this.button(this.trigger())?.focus();
      }
      this.wasOpen = open;
    });
  }

  onEscape(event: Event): void {
    // A calendar or select overlay inside the panel consumes the Escape that closes it.
    if (!event.defaultPrevented) {
      this.state().close();
    }
  }

  // The close button is the panel's first tabbable and Reset its last by construction, so the
  // modal's Tab ring is those two and the panel itself, which holds focus on open.
  onTab(event: Event, backwards: boolean): void {
    const close = this.button(this.close());
    const reset = this.button(this.reset());
    if (!close || !reset) {
      return;
    }
    const from = event.target;
    if (backwards && (from === close || from === this.panel().nativeElement)) {
      reset.focus();
      event.preventDefault();
    } else if (!backwards && from === reset) {
      close.focus();
      event.preventDefault();
    }
  }

  private button(host: ElementRef<HTMLElement>): HTMLButtonElement | null {
    return host.nativeElement.querySelector('button');
  }
}
