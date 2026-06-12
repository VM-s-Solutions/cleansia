import { Route } from '@angular/router';
import { DataProtectionComponent } from './data-protection/data-protection.component';

export const dataProtectionRoutes: Route[] = [
  {
    path: '',
    component: DataProtectionComponent,
    data: { title: 'page_titles.admin.data_protection' },
  },
];
