import { Route } from '@angular/router';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';

export const confirmEmailRoutes: Route[] = [
  {
    path: '',
    component: ConfirmEmailComponent,
    data: { title: 'page_titles.partner.confirm_email' },
  },
];
