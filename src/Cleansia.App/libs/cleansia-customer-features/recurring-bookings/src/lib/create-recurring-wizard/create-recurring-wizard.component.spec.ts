import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  PackageListItem,
  PaymentType,
  SavedAddressDto,
  ServiceListItem,
} from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
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
  countryId: null,
};

class FakeRecurringBookingsFacade {
  formData = signal<RecurringWizardFormData>({ ...RECURRING_WIZARD_INITIAL_DATA });
  pricedSelection = signal<PricedSelection>(NOTHING_PRICED);
  cashSelectable = signal(true);
  cashReason = signal<{ key: string; params: Record<string, number> } | null>(null);
  cashClearedNotice = signal(false);
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
  formPrice = signal(null);
  initialize = jest.fn();
  ensureAddresses = jest.fn();
  quoteForm = jest.fn();
  prefill = jest.fn();
  findTemplate = jest.fn(() => null);
  loadForEdit = jest.fn();
  packageName = jest.fn(() => null);
  serviceName = jest.fn(() => null);
  updateFormData = jest.fn();
  selectPayment = jest.fn();
  toggleService = jest.fn();
  togglePackage = jest.fn();
  addAddress = jest.fn();
  submit = jest.fn();
  resetWizard = jest.fn();
  toggleActive = jest.fn();
  deleteTemplate = jest.fn();
}

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
