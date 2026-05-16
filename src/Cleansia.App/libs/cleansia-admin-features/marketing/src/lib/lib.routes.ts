import { Route } from '@angular/router';
import { SitewidePushFormComponent } from './sitewide-push-form/sitewide-push-form.component';

export const marketingRoutes: Route[] = [
  {
    path: '',
    redirectTo: 'sitewide-push',
    pathMatch: 'full',
  },
  {
    path: 'sitewide-push',
    component: SitewidePushFormComponent,
    data: { title: 'page_titles.admin.sitewide_push' },
  },
];
