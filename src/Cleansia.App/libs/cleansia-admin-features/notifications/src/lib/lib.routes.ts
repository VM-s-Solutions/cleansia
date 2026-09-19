import { Route } from '@angular/router';
import { NotificationsComponent } from './notifications/notifications.component';

export const notificationsRoutes: Route[] = [
  {
    path: '',
    component: NotificationsComponent,
    data: { title: 'page_titles.admin.notifications' },
  },
];
