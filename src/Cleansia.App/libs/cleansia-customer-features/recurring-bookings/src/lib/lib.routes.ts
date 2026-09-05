import { Route } from '@angular/router';
import { customerMembershipGuard } from '@cleansia/customer-services';
import { CreateRecurringWizardComponent } from './create-recurring-wizard/create-recurring-wizard.component';
import { RecurringBookingsListComponent } from './recurring-bookings-list/recurring-bookings-list.component';

/**
 * Mounted under /membership/recurring (and .../recurring/create).
 *
 * AUTH is inherited: the customer app's `customerAuthGuard` runs at the top-level Membership
 * route, so it is not duplicated here.
 *
 * MEMBERSHIP is not inherited, and was not enforced anywhere on the client. The gate mirrors the
 * server exactly rather than blanket-guarding the folder, because the server draws a deliberate
 * line: `CreateRecurringBooking` and `UpdateRecurringBooking` demand an active membership, while
 * the list, delete and pause/resume do NOT. A lapsed member keeps full control of the schedules
 * they already have — they simply cannot add or reshape one. Guarding the list would lock them out
 * of cancelling their own recurring cleans, which is the opposite of what a lapse should do.
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
    // Server: CreateRecurringBooking returns recurring_booking.membership_required without one.
    canActivate: [customerMembershipGuard],
    data: { title: 'page_titles.customer.recurring_bookings_create' },
  },
  {
    // Editing reuses the create screen — the two differ only in which command
    // the facade sends. Registered AFTER 'create' so the literal segment wins
    // over the parameter; a schedule id is a ULID and never spells "create",
    // but relying on that rather than on route order would be a trap for the
    // next person who adds a segment here.
    path: ':id',
    component: CreateRecurringWizardComponent,
    // Server: UpdateRecurringBooking carries the same rule as create.
    canActivate: [customerMembershipGuard],
    data: { title: 'page_titles.customer.recurring_bookings_edit' },
  },
];
