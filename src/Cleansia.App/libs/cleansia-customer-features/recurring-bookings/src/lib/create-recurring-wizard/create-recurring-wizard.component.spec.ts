import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import {
  DirtinessLevel,
  PackageListItem,
  PaymentType,
  PreferredCleanerOption,
  SavedAddressDto,
  ServiceListItem,
} from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { ConfirmationService } from 'primeng/api';
import { DatePicker } from 'primeng/datepicker';
import { RecurringBookingsFacade } from '../recurring-bookings.facade';
import {
  MissingField,
  PricedSelection,
  RECURRING_WIZARD_INITIAL_DATA,
  RecurringWizardFormData,
} from '../recurring-bookings.models';
import { CreateRecurringWizardComponent } from './create-recurring-wizard.component';

const NOTHING_PRICED: PricedSelection = {
  serviceIds: [],
  packageIds: [],
  rooms: 2,
  bathrooms: 1,
  dirtinessLevel: DirtinessLevel.Normal,
  countryId: null,
};

class FakeRecurringBookingsFacade {
  formData = signal<RecurringWizardFormData>({ ...RECURRING_WIZARD_INITIAL_DATA });
  pricedSelection = signal<PricedSelection>(NOTHING_PRICED);
  cashSelectable = signal(true);
  cashReason = signal<{ key: string; params: Record<string, number> } | null>(null);
  cashClearedNotice = signal(false);
  preferredCleanerRefused = signal(false);
  servingCleanersLoading = signal(false);
  preferredCleanerVisible = signal(false);
  preferredCleanerOptions = signal<PreferredCleanerOption[]>([]);
  latestStartsOn = signal<Date | null>(null);
  packages = signal<PackageListItem[]>([]);
  services = signal<ServiceListItem[]>([]);
  savedAddresses = signal<SavedAddressDto[]>([]);
  addressesLoading = signal(false);
  editingId = signal<string | null>(null);
  listLoaded = signal(false);
  mutatingId = signal<string | null>(null);
  submitAttempted = signal(false);
  submitting = signal(false);
  missing = signal<MissingField[]>([]);
  termsAsked = signal(false);
  formPrice = signal(null);
  cardCaptureVisible = signal(false);
  cardCaptureConsent = signal(false);
  cardCaptureStarting = signal(false);
  setCardCaptureConsent = jest.fn();
  closeCardCapture = jest.fn();
  startCardCapture = jest.fn();
  restoreParkedForm = jest.fn();
  initialize = jest.fn();
  ensureAddresses = jest.fn();
  loadServingCleaners = jest.fn();
  loadConsentState = jest.fn();
  selectPreferredCleaner = jest.fn();
  quoteForm = jest.fn();
  prefill = jest.fn();
  findTemplate = jest.fn(() => null);
  loadForEdit = jest.fn();
  packageName = jest.fn(() => null);
  serviceName = jest.fn(() => null);
  packageNamesIncluding = jest.fn<string | null, [string]>(() => null);
  updateFormData = jest.fn();
  selectPayment = jest.fn();
  toggleService = jest.fn();
  togglePackage = jest.fn();
  addAddress = jest.fn();
  submit = jest.fn();
  saveWithoutPreferredCleaner = jest.fn();
  resetWizard = jest.fn();
  toggleActive = jest.fn();
  deleteTemplate = jest.fn();
}

// A package plus a service it already includes books that service on every clean twice. The row
// says so, and the add goes through the facade, which asks first.
describe('CreateRecurringWizardComponent — a service a chosen package already includes', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    facade.services.set([ServiceListItem.fromJS({ id: 'windows', name: 'Windows' })]);
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const marker = () => el.querySelector('[data-spec-in-package]');

  it('shows no line while no chosen package includes the service', () => {
    expect(marker()).toBeNull();
  });

  it('names the package inside the row, so it is read with it', () => {
    facade.packageNamesIncluding.mockImplementation((id: string) =>
      id === 'windows' ? 'Deep clean' : null,
    );
    facade.formData.update((d) => ({ ...d, selectedPackageIds: ['deep'] }));
    fixture.detectChanges();

    expect(marker()?.textContent).toContain('pages.order.package_overlap.in_package');
    expect(marker()?.closest('button')).not.toBeNull();
  });

  it('hands an add to the facade, which decides whether to ask', () => {
    // The only pick on screen: the catalogue holds one service and no package.
    el.querySelector<HTMLButtonElement>('.cl-rec__pick')?.click();

    expect(facade.toggleService).toHaveBeenCalledWith('windows');
  });
});

// Cash is refused unless one cleaner does each clean. The select is the only thing that keeps a
// refused cash pick off screen — the facade ignores it, so an enabled option would show Cash on a
// schedule saved on card.
describe('CreateRecurringWizardComponent — paying in cash', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const cashOption = () =>
    fixture.componentInstance.paymentOptions().find((option) => option.value === PaymentType.Cash);
  const cardOption = () =>
    fixture.componentInstance.paymentOptions().find((option) => option.value === PaymentType.Card);

  it('picks up a new schedule the customer left to save a card', () => {
    expect(facade.restoreParkedForm).toHaveBeenCalledWith(null);
  });

  it('disables the cash option whenever the facade says cash cannot be chosen', () => {
    facade.cashSelectable.set(false);

    expect(cashOption()?.disabled).toBe(true);
    expect(cardOption()?.disabled).toBeFalsy();
  });

  it('leaves cash open when the facade says it can be chosen', () => {
    expect(cashOption()?.disabled).toBe(false);
  });

  it('shows the reason cash is not available under the payment field', () => {
    facade.cashReason.set({ key: 'recurring_booking.cash_needs_card', params: { count: 2 } });
    fixture.detectChanges();

    expect(el.querySelector('[data-spec-cash-reason]')?.textContent).toContain(
      'recurring_booking.cash_needs_card',
    );

    facade.cashReason.set(null);
    fixture.detectChanges();

    expect(el.querySelector('[data-spec-cash-reason]')).toBeNull();
  });

  it('says cash was taken away only while the facade says so', () => {
    facade.cashClearedNotice.set(true);
    fixture.detectChanges();
    expect(el.querySelector('[data-spec-cash-cleared]')?.textContent).toContain(
      'recurring_booking.cash_cleared',
    );

    facade.cashClearedNotice.set(false);
    fixture.detectChanges();
    expect(el.querySelector('[data-spec-cash-cleared]')).toBeNull();
  });

  // The web cannot edit a schedule's end date, so a start past it could never be saved.
  it('caps the start date at the latest the facade allows', () => {
    const startPicker = () =>
      fixture.debugElement.query(By.directive(DatePicker)).componentInstance as DatePicker;
    expect(startPicker().maxDate).toBeNull();

    const latest = new Date('2027-03-30T00:00:00Z');
    facade.latestStartsOn.set(latest);
    fixture.detectChanges();

    expect(startPicker().maxDate).toEqual(latest);
  });

  it('re-quotes when what the schedule costs changes, not when the day, time or payment does', () => {
    expect(facade.quoteForm).toHaveBeenCalledTimes(1);

    facade.formData.update((d) => ({ ...d, timeOfDay: '11:00', paymentType: PaymentType.Cash }));
    fixture.detectChanges();
    expect(facade.quoteForm).toHaveBeenCalledTimes(1);

    facade.pricedSelection.set({ ...NOTHING_PRICED, serviceIds: ['s1'] });
    fixture.detectChanges();
    expect(facade.quoteForm).toHaveBeenCalledTimes(2);
  });
});

// Editing sends the schedule's preferred cleaner back. When the server no longer accepts them, the
// form says so and offers the one way through — saving without them, by the customer's own press.
describe('CreateRecurringWizardComponent — a refused preferred cleaner', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    facade.editingId.set('t1');
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const notice = () => el.querySelector('[data-spec-preferred-refused]');
  const saveWithout = () =>
    el.querySelector<HTMLButtonElement>('[data-spec-save-without-preferred]');

  it('says nothing about the preferred cleaner until the server refuses them', () => {
    expect(notice()).toBeNull();
    expect(saveWithout()).toBeNull();
  });

  it('names the refusal and offers saving without them', () => {
    facade.preferredCleanerRefused.set(true);
    fixture.detectChanges();

    expect(notice()?.textContent).toContain('preferred_cleaner.schedule_refused');
    expect(saveWithout()?.textContent).toContain('preferred_cleaner.schedule_save_without');
  });

  it('saves without them only on the press, then returns to the list', async () => {
    const router = TestBed.inject(Router);
    const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    facade.saveWithoutPreferredCleaner.mockResolvedValue(true);
    facade.preferredCleanerRefused.set(true);
    fixture.detectChanges();
    expect(facade.saveWithoutPreferredCleaner).not.toHaveBeenCalled();

    saveWithout()?.click();
    await fixture.whenStable();

    expect(facade.saveWithoutPreferredCleaner).toHaveBeenCalledTimes(1);
    expect(facade.resetWizard).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith(['/membership', 'recurring']);
  });

  it('saves without them only once the form is complete, as the ordinary save does', async () => {
    facade.missing.set(['time']);
    facade.preferredCleanerRefused.set(true);
    fixture.detectChanges();

    saveWithout()?.click();
    await fixture.whenStable();

    expect(facade.submitAttempted()).toBe(true);
    expect(facade.saveWithoutPreferredCleaner).not.toHaveBeenCalled();
  });

  it('stays on the form when saving without them is refused too', async () => {
    const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    facade.saveWithoutPreferredCleaner.mockResolvedValue(false);
    facade.preferredCleanerRefused.set(true);
    fixture.detectChanges();

    saveWithout()?.click();
    await fixture.whenStable();

    expect(facade.resetWizard).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });
});

describe('CreateRecurringWizardComponent — the favourite cleaner', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const picker = () => el.querySelector('[data-spec-preferred-picker]');

  it('asks for the cleaners who have served the customer when the form opens', () => {
    expect(facade.loadServingCleaners).toHaveBeenCalledTimes(1);
  });

  it('offers the picker only when there is someone to ask', () => {
    expect(picker()).toBeNull();

    facade.preferredCleanerVisible.set(true);
    fixture.detectChanges();

    expect(picker()?.textContent).toContain('preferred_cleaner.schedule_explainer');
  });

  it('hands a choice or a clear to the facade', () => {
    fixture.componentInstance.selectPreferredCleaner('e-1');
    fixture.componentInstance.selectPreferredCleaner(null);

    expect(facade.selectPreferredCleaner).toHaveBeenNthCalledWith(1, 'e-1');
    expect(facade.selectPreferredCleaner).toHaveBeenNthCalledWith(2, null);
  });

  it('words a refusal on a new schedule without "no longer"', () => {
    facade.preferredCleanerRefused.set(true);
    fixture.detectChanges();

    expect(el.querySelector('[data-spec-preferred-refused]')?.textContent).toContain(
      'preferred_cleaner.schedule_refused_new',
    );
    expect(el.querySelector('[data-spec-save-without-preferred]')).not.toBeNull();
  });
});

const BOOKING_POLICY = join(
  __dirname,
  '../../../../../../../Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs',
);

function bookingPolicy(name: string): number {
  const match = new RegExp(`public\\s+const\\s+int\\s+${name}\\s*=\\s*(\\d+)\\s*;`).exec(
    readFileSync(BOOKING_POLICY, 'utf8'),
  );
  if (!match) throw new Error(`BookingPolicy.${name} not found — the parser needs updating`);
  return Number(match[1]);
}

const oneTo = (max: number): number[] => Array.from({ length: max }, (_, i) => i + 1);

// The server refuses a home above BookingPolicy.MaxRooms or MaxBathrooms (order.size_exceeds_maximum),
// so the pickers offer one up to each and nothing it would refuse.
describe('CreateRecurringWizardComponent — home size', () => {
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: new FakeRecurringBookingsFacade() },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    const fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const offered = (picker: number): number[] =>
    Array.from(
      el.querySelectorAll('.cl-rec__counts > div')[picker].querySelectorAll('.cl-rec__chip--count'),
    ).map((chip) => Number(chip.textContent?.trim()));

  it('offers one room up to the server maximum', () => {
    expect(offered(0)).toEqual(oneTo(bookingPolicy('MaxRooms')));
  });

  it('offers one bathroom up to the server maximum', () => {
    expect(offered(1)).toEqual(oneTo(bookingPolicy('MaxBathrooms')));
  });
});

// Decision 34: a schedule carries the level its customer picks, from the same three the booking
// wizard offers, and a new one is asked rather than assumed.
describe('CreateRecurringWizardComponent — how clean the home is', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const levels = () => Array.from(el.querySelectorAll<HTMLButtonElement>('[data-spec-level]'));
  const pressed = () => levels().map((level) => level.getAttribute('aria-pressed'));

  it('offers the three levels with the booking copy and none chosen on a new schedule', () => {
    expect(levels().map((level) => level.querySelector('.cl-wiz__level-name')?.textContent?.trim())).toEqual([
      'pages.order.dirtiness.normal.name',
      'pages.order.dirtiness.increased.name',
      'pages.order.dirtiness.heavy.name',
    ]);
    expect(pressed()).toEqual(['false', 'false', 'false']);
  });

  it('hands the level tapped to the facade', () => {
    levels()[1].click();

    expect(facade.updateFormData).toHaveBeenCalledWith({ dirtinessLevel: DirtinessLevel.Increased });
  });

  it('shows the level the schedule has as the chosen one', () => {
    facade.formData.update((data) => ({ ...data, dirtinessLevel: DirtinessLevel.Heavy }));
    fixture.detectChanges();

    expect(pressed()).toEqual(['false', 'false', 'true']);
  });

  it('names a missing level only once save has been pressed', () => {
    facade.missing.set(['dirtiness']);
    fixture.detectChanges();
    expect(el.querySelector('[data-spec-dirtiness-error]')).toBeNull();

    facade.submitAttempted.set(true);
    fixture.detectChanges();

    expect(el.querySelector('[data-spec-dirtiness-error]')?.textContent).toContain(
      'recurring_booking.error_dirtiness',
    );
    expect(fixture.componentInstance.missingLabels()).toBe('recurring_booking.dirtiness_label');
  });
});

describe('CreateRecurringWizardComponent — the request to start within the withdrawal period', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  beforeEach(async () => {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  const tick = () => el.querySelector<HTMLElement>('[data-spec-early-performance]');

  it('is asked once, on a new schedule, in the wording the server records', () => {
    expect(tick()?.textContent).toContain(
      'pages.order.early_performance.early-performance-draft-2026-09-29',
    );
  });

  it('is not asked when a schedule is edited', () => {
    facade.editingId.set('t1');
    fixture.detectChanges();

    expect(tick()).toBeNull();
  });

  it('hands the answer to the facade', () => {
    const checkbox = fixture.debugElement.query(By.css('[data-spec-early-performance] p-checkbox'));
    checkbox.triggerEventHandler('ngModelChange', true);

    expect(facade.updateFormData).toHaveBeenCalledWith({ earlyPerformanceRequested: true });
  });

  it('names the missing request only once save has been pressed', () => {
    facade.missing.set(['earlyPerformance']);
    fixture.detectChanges();
    expect(el.querySelector('[data-spec-early-performance-error]')).toBeNull();

    facade.submitAttempted.set(true);
    fixture.detectChanges();

    expect(el.querySelector('[data-spec-early-performance-error]')?.textContent).toContain(
      'recurring_booking.error_early_performance',
    );
    expect(fixture.componentInstance.missingLabels()).toBe(
      'recurring_booking.early_performance_label',
    );
  });
});

describe('CreateRecurringWizardComponent — the terms tick', () => {
  let fixture: ComponentFixture<CreateRecurringWizardComponent>;
  let facade: FakeRecurringBookingsFacade;
  let el: HTMLElement;

  async function open(scheduleId: string | null): Promise<void> {
    facade = new FakeRecurringBookingsFacade();
    await TestBed.configureTestingModule({
      imports: [CreateRecurringWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: (key: string) => (key === 'id' ? scheduleId : null) },
              queryParamMap: { get: () => null },
            },
          },
        },
      ],
    })
      .overrideComponent(CreateRecurringWizardComponent, {
        set: {
          providers: [
            { provide: RecurringBookingsFacade, useValue: facade },
            ConfirmationService,
          ],
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(CreateRecurringWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  }

  const tick = () => el.querySelector<HTMLElement>('[data-spec-terms]');

  it('reads the consents on record when a schedule is created', async () => {
    await open(null);

    expect(facade.loadConsentState).toHaveBeenCalledTimes(1);
  });

  it('reads no consents when a schedule is edited', async () => {
    await open('t1');

    expect(facade.loadConsentState).not.toHaveBeenCalled();
  });

  it('is shown only while the facade asks for it, in the booking wording', async () => {
    await open(null);
    expect(tick()).toBeNull();

    facade.termsAsked.set(true);
    fixture.detectChanges();

    expect(tick()?.textContent).toContain('pages.order.accept_terms');
  });

  it('hands the answer to the facade', async () => {
    await open(null);
    facade.termsAsked.set(true);
    fixture.detectChanges();

    fixture.debugElement
      .query(By.css('[data-spec-terms] p-checkbox'))
      .triggerEventHandler('ngModelChange', true);

    expect(facade.updateFormData).toHaveBeenCalledWith({ termsAccepted: true });
  });

  it('names the missing tick only once save has been pressed', async () => {
    await open(null);
    facade.termsAsked.set(true);
    facade.missing.set(['terms']);
    fixture.detectChanges();
    expect(el.querySelector('[data-spec-terms-error]')).toBeNull();

    facade.submitAttempted.set(true);
    fixture.detectChanges();

    expect(el.querySelector('[data-spec-terms-error]')?.textContent).toContain(
      'pages.order.missing.terms',
    );
    expect(fixture.componentInstance.missingLabels()).toBe('recurring_booking.terms_label');
  });
});
