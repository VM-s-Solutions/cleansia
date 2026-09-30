import { Route } from '@angular/router';
import { HowJobsAreOfferedComponent } from './how-jobs-are-offered/how-jobs-are-offered.component';
import { ProfileComponent } from './profile/profile.component';

export const profileRoutes: Route[] = [
  {
    path: '',
    component: ProfileComponent,
    data: { title: 'page_titles.partner.profile' },
  },
];

export const howJobsAreOfferedRoutes: Route[] = [
  {
    path: '',
    component: HowJobsAreOfferedComponent,
    data: { title: 'page_titles.partner.how_jobs_are_offered' },
  },
];
