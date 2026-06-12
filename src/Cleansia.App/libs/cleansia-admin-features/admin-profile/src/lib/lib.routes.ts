import { Route } from '@angular/router';
import { AdminProfileComponent } from './admin-profile/admin-profile.component';

export const adminProfileRoutes: Route[] = [
  {
    path: '',
    component: AdminProfileComponent,
    data: { title: 'page_titles.admin.profile' },
  },
];
