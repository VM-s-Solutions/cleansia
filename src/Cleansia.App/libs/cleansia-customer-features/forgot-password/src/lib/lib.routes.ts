import { Route } from '@angular/router';
import { ForgotPasswordComponent } from './forgot-password';

export const forgotPasswordRoutes: Route[] = [
  {
    path: '',
    component: ForgotPasswordComponent,
    data: { title: 'page_titles.customer.forgot_password' },
  },
];
