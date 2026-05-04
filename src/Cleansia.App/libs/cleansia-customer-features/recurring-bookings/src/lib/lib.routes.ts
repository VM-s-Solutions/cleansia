import { Route } from '@angular/router';
import { CreateRecurringWizardComponent } from './create-recurring-wizard/create-recurring-wizard.component';
import { RecurringBookingsListComponent } from './recurring-bookings-list/recurring-bookings-list.component';

/**
 * Plus-only — mounted under /membership/recurring (and .../recurring/create).
 * The customer app's auth guard runs at the top-level Membership route, so
 * these inherit it transitively; we don't duplicate the guard here.
 */
export const recurringBookingsRoutes: Route[] = [
  {
    path: '',
    component: RecurringBookingsListComponent,
    data: { title: 'page_titles.customer.recurring_bookings' },
  },
  {
    path: 'create',
    component: CreateRecurringWizardComponent,
    data: { title: 'page_titles.customer.recurring_bookings_create' },
  },
];
