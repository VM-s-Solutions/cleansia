import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { FilterDrawerState } from './filter-drawer-state';

/**
 * The active-filter chip row under a list page's header: one chip per active filter, each with a
 * remove button named after the chip's text, and a clear-all once anything is active.
 */
@Component({
  selector: 'cleansia-filter-chips',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './cleansia-filter-chips.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaFilterChipsComponent {
  state = input.required<FilterDrawerState>();
}
