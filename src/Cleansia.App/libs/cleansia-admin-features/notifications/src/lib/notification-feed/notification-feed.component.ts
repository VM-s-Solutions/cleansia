import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { NotificationRow } from '../notifications/notifications.models';

@Component({
  selector: 'cleansia-admin-notification-feed',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './notification-feed.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationFeedComponent {
  readonly rows = input.required<NotificationRow[]>();
  readonly openingId = input<string | null>(null);
  readonly refreshing = input<boolean>(false);
  readonly rowOpen = output<NotificationRow>();

  onRowKeydown(row: NotificationRow, event: Event): void {
    event.preventDefault();
    this.rowOpen.emit(row);
  }
}
