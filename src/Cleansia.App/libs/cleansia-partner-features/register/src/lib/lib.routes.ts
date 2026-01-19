import { Route } from '@angular/router';
import { RegisterComponent } from './register/register.component';

export const registerRoutes: Route[] = [
  {
    path: '',
    component: RegisterComponent,
    data: { title: 'page_titles.partner.register' },
  },
];
