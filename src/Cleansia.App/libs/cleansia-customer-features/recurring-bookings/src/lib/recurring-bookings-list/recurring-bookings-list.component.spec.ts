import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CustomerClient, RecurringBookingTemplateDto } from '@cleansia/customer-services';
import {
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerPackagesCatalogue,
  selectCustomerServices,
  selectCustomerServicesCatalogue,
  selectMarketCountryId,
} from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { ConfirmationService } from 'primeng/api';
import { RecurringBookingsFacade } from '../recurring-bookings.facade';
import { RecurringBookingsListComponent } from './recurring-bookings-list.component';

class FakeRecurringBookingsFacade {
  templates = signal<RecurringBookingTemplateDto[]>([]);
  listLoading = signal(false);
  listLoaded = signal(true);
  isMember = signal(true);
  membershipLoaded = signal(true);
  templatePrices = signal({});
  initialize = jest.fn();
  quoteTemplate = jest.fn();
  serviceName = jest.fn(() => null);
  packageName = jest.fn(() => null);
  nextRun = jest.fn((): Date | null => null);
  holdsRetiredEntry = jest.fn(() => false);
}

const schedule = (id: string, requiresPaymentMethodChange: boolean) =>
  RecurringBookingTemplateDto.fromJS({
    id,
    frequency: 1,
    dayOfWeek: 3,
    timeOfDay: '10:00',
    rooms: 2,
    bathrooms: 1,
    paymentType: 1,
    isActive: true,
    requiresPaymentMethodChange,
  });

// A legacy cash schedule that now needs more than one cleaner is skipped by the materializer rather
// than moved to card, so the list is where the customer learns it and how to put it right.
describe('RecurringBookingsListComponent — a schedule that can no longer be paid in cash', () => {
  let fixture: ComponentFixture<RecurringBookingsListComponent>;
  let facade: FakeRecurringBookingsFacade;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [RecurringBookingsListComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    })
      .overrideComponent(RecurringBookingsListComponent, {
        set: { providers: [{ provide: RecurringBookingsFacade, useValue: facade }] },
      })
      .compileComponents();
    fixture = TestBed.createComponent(RecurringBookingsListComponent);
  });

  const cards = (): HTMLElement[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.cl-rec__card'));

  it('says why, and links to the schedule to change it', () => {
    facade.templates.set([schedule('t-cash', true)]);
    fixture.detectChanges();

    const card = cards()[0];
    expect(card.querySelector('[data-spec-cash-change]')?.textContent).toContain(
      'recurring_booking.cash_change_title',
    );
    expect(card.textContent).toContain('recurring_booking.cash_change_body');
    const links = Array.from(card.querySelectorAll('a')).map((a) => a.getAttribute('href'));
    expect(links.filter((href) => href === '/membership/recurring/t-cash').length).toBe(2);
  });

  it('says nothing on a schedule that needs no change', () => {
    facade.templates.set([schedule('t-card', false)]);
    fixture.detectChanges();

    expect(cards()[0].querySelector('[data-spec-cash-change]')).toBeNull();
    expect(cards()[0].textContent).not.toContain('recurring_booking.cash_change_body');
  });

  it('promises no next cleaning and does not call the schedule active', () => {
    facade.nextRun.mockReturnValue(new Date('2026-10-07T10:00:00Z'));
    facade.templates.set([schedule('t-cash', true)]);
    fixture.detectChanges();

    const card = cards()[0];
    expect(card.textContent).not.toContain('recurring_booking.next_on');
    const pill = card.querySelector('.cl-rec__pill') as HTMLElement;
    expect(pill.textContent?.trim()).toBe('recurring_booking.status_needs_change');
    expect(pill.classList).toContain('cl-rec__pill--off');
    expect(pill.classList).not.toContain('cl-rec__pill--on');
  });

  it('promises the next cleaning of an active schedule that needs no change', () => {
    facade.nextRun.mockReturnValue(new Date('2026-10-07T10:00:00Z'));
    facade.templates.set([schedule('t-card', false)]);
    fixture.detectChanges();

    const card = cards()[0];
    expect(card.textContent).toContain('recurring_booking.next_on');
    const pill = card.querySelector('.cl-rec__pill') as HTMLElement;
    expect(pill.textContent?.trim()).toBe('recurring_booking.status_active');
    expect(pill.classList).toContain('cl-rec__pill--on');
  });
});

// The server refuses to price a schedule holding a retired entry, so its card has no price; this
// line is why. The facade decides; the card only says it.
describe('RecurringBookingsListComponent — a schedule holding something no longer offered', () => {
  let fixture: ComponentFixture<RecurringBookingsListComponent>;
  let facade: FakeRecurringBookingsFacade;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [RecurringBookingsListComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    })
      .overrideComponent(RecurringBookingsListComponent, {
        set: { providers: [{ provide: RecurringBookingsFacade, useValue: facade }] },
      })
      .compileComponents();
    fixture = TestBed.createComponent(RecurringBookingsListComponent);
  });

  const line = (): Element | null =>
    (fixture.nativeElement as HTMLElement).querySelector('.cl-rec__card [data-spec-retired]');

  it('says so on its card', () => {
    facade.holdsRetiredEntry.mockReturnValue(true);
    facade.templates.set([schedule('t-retired', false)]);
    fixture.detectChanges();

    expect(facade.holdsRetiredEntry).toHaveBeenCalledWith(facade.templates()[0]);
    expect(line()?.textContent?.trim()).toBe('recurring_booking.card_item_no_longer_offered');
  });

  it('says nothing on a schedule that holds nothing retired', () => {
    facade.templates.set([schedule('t-listed', false)]);
    fixture.detectChanges();

    expect(line()).toBeNull();
  });
});

describe('RecurringBookingsListComponent — the next cleaning date', () => {
  let fixture: ComponentFixture<RecurringBookingsListComponent>;
  let facade: FakeRecurringBookingsFacade;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [RecurringBookingsListComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    })
      .overrideComponent(RecurringBookingsListComponent, {
        set: { providers: [{ provide: RecurringBookingsFacade, useValue: facade }] },
      })
      .compileComponents();
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', { recurring_booking: { next_on: 'next {{date}}' } }, true);
    translate.use('en');
    fixture = TestBed.createComponent(RecurringBookingsListComponent);
  });

  it('is the date in the market the schedule names, the same day as its weekday', () => {
    // Wednesday 7 October, 08:00 at UTC+14, is still 6 October in UTC and in Prague.
    facade.nextRun.mockReturnValue(new Date('2026-10-06T18:00:00Z'));
    facade.templates.set([
      RecurringBookingTemplateDto.fromJS({
        id: 't-kiritimati',
        frequency: 3,
        dayOfWeek: 3,
        timeOfDay: '08:00',
        rooms: 2,
        bathrooms: 1,
        paymentType: 1,
        isActive: true,
        requiresPaymentMethodChange: false,
        timeZoneId: 'Pacific/Kiritimati',
      }),
    ]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).querySelector('.cl-rec__card')?.textContent;
    expect(text).toContain('next October 7');
    expect(text).not.toContain('October 6');
  });
});

// The specs above hand the screen a fake facade, so only this one sees whether the providers the screen
// declares can build the real one; a missing provider is an error the moment the list opens.
describe('RecurringBookingsListComponent — its own facade', () => {
  it('builds the facade from the providers the screen declares', async () => {
    await TestBed.configureTestingModule({
      imports: [RecurringBookingsListComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideMockStore({
          selectors: [
            { selector: selectCustomerServices, value: [] },
            { selector: selectCustomerPackages, value: [] },
            { selector: selectCustomerServicesCatalogue, value: { services: [], countryId: null } },
            { selector: selectCustomerPackagesCatalogue, value: { packages: [], countryId: null } },
            { selector: selectMarketCountryId, value: null },
          ],
        }),
        { provide: CustomerClient, useValue: {} },
        { provide: SavedAddressStore, useValue: { addresses: signal([]), loaded: signal(true) } },
        { provide: SnackbarService, useValue: {} },
        // The app root's, behind the shared confirm the facade asks through.
        ConfirmationService,
        // The app root's, under the toast-suppressing client the facade quotes through.
        provideHttpClient(),
      ],
    }).compileComponents();

    expect(() => TestBed.createComponent(RecurringBookingsListComponent)).not.toThrow();
  });
});

// jsdom loads no stylesheet, so this reads the badge's rule, which is declared an input of this test
// target. The badge is --cl-heading: Sky700 light, Sky300 dark; white on Sky300 measured 1.7.
describe('RecurringBookingsListComponent — the Plus badge on the paywall', () => {
  it('inks the badge with the card ground, which flips with the slab', () => {
    const scss = readFileSync(
      join(__dirname, '../../../../../shared/assets/src/styles/pages/cleansia-customer/_recurring-bookings.scss'),
      'utf8',
    );
    const badge = scss.match(/^\.cl-rec__gate-badge \{[^}]*\}/m)?.[0] ?? '';
    expect(badge).toContain('background: var(--cl-heading);');
    expect(badge).toContain('color: var(--cl-surface);');
  });
});
