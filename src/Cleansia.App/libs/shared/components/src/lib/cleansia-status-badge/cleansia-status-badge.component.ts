import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { resolveStatusBadge, StatusBadgeKind, StatusBadgeValue } from './cleansia-status-badge.models';

/**
 * The one status pill. A page names the kind of status and hands over what the wire carried; the
 * tone and the translated label come from the shared catalogue, so two pages can never colour or
 * word the same status differently.
 *
 * @example
 * <cleansia-status-badge kind="order" [value]="order.orderStatus" />
 */
@Component({
  selector: 'cleansia-status-badge',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './cleansia-status-badge.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaStatusBadgeComponent {
  kind = input.required<StatusBadgeKind>();
  value = input<StatusBadgeValue>(null);

  readonly resolved = computed(() => resolveStatusBadge(this.kind(), this.value()));
}
