import { Route } from '@angular/router';
import { ProfileComponent } from './profile/profile.component';

export const profileRoutes: Route[] = [
  {
    path: '',
    component: ProfileComponent,
    data: { title: 'page_titles.partner.profile' },
  },
];
