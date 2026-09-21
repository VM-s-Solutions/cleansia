import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  HostListener,
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
  private wasOpen = false;

  constructor() {
    effect(() => {
      const open = this.state().isOpen();
      if (open) {
        this.panel().nativeElement.focus();
      } else if (this.wasOpen) {
        this.trigger().nativeElement.querySelector('button')?.focus();
      }
      this.wasOpen = open;
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.state().isOpen()) {
      this.state().close();
    }
  }
}
