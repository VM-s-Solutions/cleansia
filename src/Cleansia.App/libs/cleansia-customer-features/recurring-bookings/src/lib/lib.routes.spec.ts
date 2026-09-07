import { customerMembershipGuard } from '@cleansia/customer-services';
import { recurringBookingsRoutes } from './lib.routes';

/**
 * Which recurring screens need Cleansia Plus — and, just as deliberately, which do not.
 *
 * The server draws the line, and the client must not draw a different one:
 *   CreateRecurringBooking  → membership required  (src/…/Bookings/CreateRecurringBooking.cs:118)
 *   UpdateRecurringBooking  → membership required  (src/…/Bookings/UpdateRecurringBooking.cs:62)
 *   GetMyRecurringBookings  → NOT required
 *   DeleteRecurringBooking  → NOT required
 *   SetRecurringBookingActive → NOT required
 *
 * The asymmetry is the point: a member whose subscription lapses keeps full control of the
 * schedules they already have — they can see them, pause them and delete them — and simply cannot
 * add or reshape one. Guarding the list "for consistency" would lock them out of cancelling their
 * own recurring cleans, which is the opposite of what a lapse should do. That is the mistake this
 * test exists to catch.
 */
describe('recurringBookingsRoutes — where Plus is required', () => {
  const route = (path: string) => recurringBookingsRoutes.find((r) => r.path === path);

  it('gates CREATE on an active membership', () => {
    expect(route('create')?.canActivate).toContain(customerMembershipGuard);
  });

  it('gates EDIT on an active membership, exactly as the update command does', () => {
    expect(route(':id')?.canActivate).toContain(customerMembershipGuard);
  });

  it('leaves the LIST open, so a lapsed member can still cancel what they have', () => {
    expect(route('')?.canActivate ?? []).not.toContain(customerMembershipGuard);
  });

  it("keeps 'create' ahead of ':id' so the literal segment wins", () => {
    const paths = recurringBookingsRoutes.map((r) => r.path);

    expect(paths.indexOf('create')).toBeLessThan(paths.indexOf(':id'));
  });
});
