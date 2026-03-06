import { Route } from '@angular/router';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';

export const confirmEmailRoutes: Route[] = [
  {
    path: '',
    component: ConfirmEmailComponent,
    data: { title: 'page_titles.customer.confirm_email' },
  },
];
