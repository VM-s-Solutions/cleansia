import { Route } from '@angular/router';

export const notificationsRoutes: Route[] = [
  {
    path: '',
    loadComponent: () =>
      import('./notifications/notifications.component').then(
        (m) => m.NotificationsComponent
      ),
    data: { title: 'page_titles.admin.notifications' },
  },
];
