import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { computed, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SnackbarService } from '@cleansia/services';
import { PaymentType, ServiceListItem, PackageListItem } from '@cleansia/partner-services';
import { AddressDto, SavedAddressDto } from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { OrderWizardComponent } from './order-wizard.component';
import { OrderWizardFacade } from './order-wizard.facade';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

function makeService(id: string, name: string): ServiceListItem {
  return ServiceListItem.fromJS({ id, name, basePrice: 100, perRoomPrice: 0 });
}

function makePackage(id: string, name: string): PackageListItem {
  return PackageListItem.fromJS({ id, name, price: 200, includedServices: [] });
}

function makeAddress(id: string, label: string): SavedAddressDto {
  return SavedAddressDto.fromJS({
    id,
    label,
    street: 'Main 1',
    city: 'Prague',
    zipCode: '11000',
    isDefault: false,
  });
}

/**
 * Lightweight stand-in for the real facade. Exposes only the signals and
 * methods the wizard template reads — the a11y specs drive selection/state via
 * these so we never spin up the Store / HTTP dependency graph.
 */
class FakeOrderWizardFacade {
  steps = [
    'pages.order.steps.services',
    'pages.order.steps.address',
    'pages.order.steps.datetime',
    'pages.order.steps.payment',
    'pages.order.steps.summary',
  ];
  stepIcons = ['pi pi-list', 'pi pi-map-marker', 'pi pi-calendar', 'pi pi-credit-card', 'pi pi-check-circle'];

  activeStep = signal(0);
  formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA, address: new AddressDto({ street: '', city: '', zipCode: '', countryId: '', state: '' }) });
  submitting = signal(false);
  selectedCategorySlug = signal<string | null>(null);

  services = signal<ServiceListItem[]>([]);
  packages = signal<PackageListItem[]>([]);
  categories = signal<unknown[]>([]);
  filteredServices = computed(() => this.services());

  isAuthenticated = signal(false);
  savedAddresses = signal<SavedAddressDto[]>([]);
  selectedSavedAddressId = signal<string | null>(null);

  cityServiced = signal<'idle' | 'pending' | 'ok' | 'rejected' | 'error'>('idle');
  extras = signal<unknown[]>([]);

  totalPrice = signal(0);
  displayedTotalPrice = signal(0);
  membershipDiscount = signal(0);
  tierDiscount = signal(0);
  effectivePromoDiscount = signal(0);
  expressSurcharge = signal(0);
  isExpressSlot = signal(false);
  appliedDiscountKind = signal<'none' | 'membership' | 'tier' | 'combined' | 'promo'>('none');
  promoCode = signal('');

  initialize = jest.fn();
  goToStep = jest.fn((step: number) => this.activeStep.set(step));
  setCategory = jest.fn((slug: string | null) => this.selectedCategorySlug.set(slug));
  selectSavedAddress = jest.fn();
  prevStep = jest.fn();
  nextStep = jest.fn();
  canProceed = jest.fn(() => true);

  updateFormData = jest.fn((patch: Partial<OrderWizardFormData>) => {
    this.formData.update((d) => ({ ...d, ...patch }));
  });
  prefillFromRebook = jest.fn(() => [] as string[]);
  applyAddressSuggestion = jest.fn();
  submitOrder = jest.fn();
}

describe('OrderWizardComponent (a11y)', () => {
  let fixture: ComponentFixture<OrderWizardComponent>;
  let facade: FakeOrderWizardFacade;
  let el: HTMLElement;

  async function setup(): Promise<void> {
    facade = new FakeOrderWizardFacade();
    await TestBed.configureTestingModule({
      imports: [OrderWizardComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideNoopAnimations(),
        { provide: SnackbarService, useValue: { showError: jest.fn(), showSuccess: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => null } } },
        },
      ],
    })
      .overrideComponent(OrderWizardComponent, {
        set: { providers: [{ provide: OrderWizardFacade, useValue: facade }] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(OrderWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  }

  // ─── AC1 + AC2 — stepper ────────────────────────────────────
  describe('stepper (AC1, AC2)', () => {
    it('renders completed stepper items as focusable native buttons', async () => {
      await setup();
      facade.activeStep.set(2);
      fixture.detectChanges();

      const items = el.querySelectorAll('.order-wizard__stepper-item');
      const completed = items[0] as HTMLElement;
      expect(completed.tagName).toBe('BUTTON');
      expect(completed.hasAttribute('disabled')).toBe(false);
    });

    it('marks the active stepper item with aria-current="step"', async () => {
      await setup();
      facade.activeStep.set(1);
      fixture.detectChanges();

      const items = el.querySelectorAll('.order-wizard__stepper-item');
      expect(items[1].getAttribute('aria-current')).toBe('step');
      expect(items[0].getAttribute('aria-current')).toBeNull();
    });

    it('navigates to a completed step via keyboard activation (native button = Enter+Space)', async () => {
      await setup();
      facade.activeStep.set(2);
      fixture.detectChanges();

      const completed = el.querySelector('.order-wizard__stepper-item') as HTMLButtonElement;
      completed.click();
      expect(facade.goToStep).toHaveBeenCalledWith(0);
    });
  });

  // ─── AC1 + AC2 — service / package cards ────────────────────
  describe('selection cards (AC1, AC2)', () => {
    it('renders service cards as focusable buttons with aria-pressed reflecting selection', async () => {
      await setup();
      facade.services.set([makeService('s-1', 'Deep clean')]);
      fixture.detectChanges();

      const card = el.querySelector('.order-wizard__selection-card') as HTMLElement;
      expect(card.tagName).toBe('BUTTON');
      expect(card.getAttribute('aria-pressed')).toBe('false');

      card.click();
      fixture.detectChanges();
      expect(card.getAttribute('aria-pressed')).toBe('true');
    });

    it('renders package cards as focusable buttons with aria-pressed', async () => {
      await setup();
      facade.packages.set([makePackage('p-1', 'Bundle')]);
      fixture.detectChanges();

      const cards = el.querySelectorAll('.order-wizard__selection-card');
      const pkgCard = cards[cards.length - 1] as HTMLElement;
      expect(pkgCard.tagName).toBe('BUTTON');
      expect(pkgCard.getAttribute('aria-pressed')).toBe('false');
    });
  });

  // ─── AC5 — counter buttons ──────────────────────────────────
  describe('room/bathroom counters (AC5)', () => {
    it('gives every counter button an aria-label', async () => {
      await setup();
      const counterBtns = el.querySelectorAll('.order-wizard__counter-btn');
      expect(counterBtns.length).toBeGreaterThanOrEqual(4);
      counterBtns.forEach((b) => {
        expect(b.getAttribute('aria-label')).toBeTruthy();
      });
    });
  });

  // ─── AC3 + AC4 — contact fields ─────────────────────────────
  describe('contact step labels + errors (AC3, AC4)', () => {
    it('associates each contact label with its input via for/id', async () => {
      await setup();
      facade.activeStep.set(1);
      fixture.detectChanges();

      const firstNameInput = el.querySelector('#wizard-first-name');
      const firstNameLabel = el.querySelector('label[for="wizard-first-name"]');
      expect(firstNameInput).toBeTruthy();
      expect(firstNameLabel).toBeTruthy();
    });

    it('sets aria-invalid + aria-describedby when a contact field has a touched error', async () => {
      await setup();
      facade.activeStep.set(1);
      fixture.detectChanges();

      fixture.componentInstance.markTouched('customerFirstName');
      fixture.detectChanges();

      const input = el.querySelector('#wizard-first-name') as HTMLElement;
      expect(input.getAttribute('aria-invalid')).toBe('true');
      const describedBy = input.getAttribute('aria-describedby');
      expect(describedBy).toBeTruthy();
      expect(el.querySelector('#' + describedBy)).toBeTruthy();
    });

    it('clears aria-invalid once the field becomes valid', async () => {
      await setup();
      facade.activeStep.set(1);
      facade.updateFormData({ customerFirstName: 'Jane' });
      fixture.detectChanges();
      fixture.componentInstance.markTouched('customerFirstName');
      fixture.detectChanges();

      const input = el.querySelector('#wizard-first-name') as HTMLElement;
      expect(input.getAttribute('aria-invalid')).not.toBe('true');
    });
  });

  // ─── AC1 + AC2 — saved address rows ─────────────────────────
  describe('saved address rows (AC1, AC2)', () => {
    it('renders saved address rows as focusable buttons with aria-pressed', async () => {
      await setup();
      facade.activeStep.set(1);
      facade.isAuthenticated.set(true);
      facade.savedAddresses.set([makeAddress('a-1', 'Home')]);
      facade.selectedSavedAddressId.set('a-1');
      fixture.detectChanges();

      const row = el.querySelector('.order-wizard__saved-address') as HTMLElement;
      expect(row.tagName).toBe('BUTTON');
      expect(row.getAttribute('aria-pressed')).toBe('true');
    });
  });

  // ─── AC1 + AC2 — payment cards ──────────────────────────────
  describe('payment cards (AC1, AC2)', () => {
    it('renders payment cards as focusable buttons with aria-pressed reflecting the chosen method', async () => {
      await setup();
      facade.activeStep.set(3);
      facade.formData.update((d) => ({ ...d, paymentType: PaymentType.Card }));
      fixture.detectChanges();

      const cards = el.querySelectorAll('.order-wizard__payment-card');
      expect(cards.length).toBe(2);
      expect((cards[0] as HTMLElement).tagName).toBe('BUTTON');
      expect(cards[0].getAttribute('aria-pressed')).toBe('true');
      expect(cards[1].getAttribute('aria-pressed')).toBe('false');
    });
  });

  // ─── AC1 — mobile price header ──────────────────────────────
  describe('mobile price header (AC1)', () => {
    it('renders the mobile price header as a focusable button with aria-expanded', async () => {
      await setup();
      const header = el.querySelector('.order-wizard__mobile-price-header') as HTMLElement;
      expect(header.tagName).toBe('BUTTON');
      expect(header.getAttribute('aria-expanded')).toBe('false');

      header.click();
      fixture.detectChanges();
      expect(header.getAttribute('aria-expanded')).toBe('true');
    });
  });
});
