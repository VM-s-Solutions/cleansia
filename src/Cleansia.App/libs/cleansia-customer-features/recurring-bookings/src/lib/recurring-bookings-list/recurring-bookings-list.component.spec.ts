import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RecurringBookingTemplateDto } from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
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
