import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CleansiaNotFoundComponent, NotFoundLink } from '@cleansia/components';
import { CleansiaCustomerRoute } from '@cleansia/services';

/**
 * The customer app's 404.
 *
 * `CleansiaNotFoundComponent` is shared with the partner and admin apps, so the
 * ways out of a dead end cannot live inside it — "Cleansia Plus" and "track your
 * order" are nonsense on an admin console. This is the customer app naming its
 * own, which is the only place that knows them.
 *
 * The routes are read off `CleansiaCustomerRoute` rather than written as
 * strings: a 404 offering a link that is itself a 404 is a particular kind of
 * bad. → the "Návratové a právní stránky" board
 */
@Component({
  selector: 'cleansia-customer-not-found',
  standalone: true,
  imports: [CleansiaNotFoundComponent],
  template:
    '<cleansia-not-found mascotSrc="assets/images/mascot/mascot-idea-tile.webp" [links]="links" />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerNotFoundComponent {
  protected readonly links: readonly NotFoundLink[] = [
    { labelKey: 'nav.services', route: '/' + CleansiaCustomerRoute.SERVICES },
    // A section of the home page, not a route of its own — the navbar links it
    // the same way.
    { labelKey: 'nav.how_it_works', route: '/', fragment: 'why-us' },
    { labelKey: 'nav.plus', route: '/' + CleansiaCustomerRoute.PLUS },
    { labelKey: 'nav.track_order', route: '/' + CleansiaCustomerRoute.TRACK_ORDER },
  ];
}
