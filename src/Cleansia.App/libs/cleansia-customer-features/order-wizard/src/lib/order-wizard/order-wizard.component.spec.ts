import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { computed, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SnackbarService } from '@cleansia/services';
import {
  AddressDto,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  PackageListItem,
  PaymentType,
  QuoteOrderResponse,
  QuotePlusSavingsResponse,
  SavedAddressDto,
  ServiceListItem,
} from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { OrderWizardComponent } from './order-wizard.component';
import { OrderWizardFacade } from './order-wizard.facade';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData, createAddressDto } from './order-wizard.models';

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
  formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA, address: createAddressDto({ street: '', city: '', zipCode: '', countryId: '', state: '' }) });
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
  preSurchargeSubtotal = signal(0);
  displayedTotalPrice = signal(0);
  currencyCode = signal<string | null>('CZK');
  // Credit lines in the summary panel. Zero by default: the overwhelming majority of these
  // cases are about a customer who has never been credited, and that is what zero renders as.
  creditBalance = signal(0);
  creditApplied = signal(0);
  amountDueOnCard = signal(0);
  membershipDiscount = signal(0);
  tierDiscount = signal(0);
  effectivePromoDiscount = signal(0);
  expressSurcharge = signal(0);
  expressSurchargeApplied = signal(false);
  expressSurchargeWaived = signal(false);
  expressUpgradesRemaining = signal(0);
  expressWaiverAvailable = signal(false);
  expressWaiverExhausted = signal(false);
  expressWaiverPendingTrial = signal(false);
  appliedDiscountKind = signal<'none' | 'membership' | 'tier' | 'combined' | 'promo'>('none');
  // The summary rail reads the server's quote for the duration estimate, so the
  // double needs it or every template render throws before an assertion runs.
  quote = signal<QuoteOrderResponse | null>(null);
  promoCode = signal('');
  promoCodeState = signal<{ kind: string; discount?: number; error?: string | null }>({ kind: 'idle' });
  clearPromoCode = jest.fn(() => {
    this.promoCode.set('');
    this.promoCodeState.set({ kind: 'idle' });
  });

  initialize = jest.fn();
  // The Plus step reads the plan catalogue and the savings preview; the double
  // supplies both so the component can render without either.
  plans = signal<GetMembershipPlansResponse[]>([]);
  plusSavings = signal<QuotePlusSavingsResponse | null>(null);
  activeMembership = signal<GetMyMembershipResponse | null>(null);
  loadPlans = jest.fn();
  loadPlusSavings = jest.fn();
  goToStep = jest.fn((step: number) => this.activeStep.set(step));
  setCategory = jest.fn((slug: string | null) => this.selectedCategorySlug.set(slug));
  selectSavedAddress = jest.fn();
  prevStep = jest.fn();
  nextStep = jest.fn();
  // Signal-backed, not a bare jest.fn: the component reads this through a
  // `computed`, and a computed with no signal dependency caches its first value
  // forever. A mockReturnValue set after the first render then changed nothing,
  // and a test asserting "the panel is absent" passed because the list was
  // stale-empty rather than because the code was right.
  missingReasonsValue = signal<string[]>([]);
  missingReasons = jest.fn((): string[] => this.missingReasonsValue());
  canProceed = jest.fn(() => this.missingReasons().length === 0);

  updateFormData = jest.fn((patch: Partial<OrderWizardFormData>) => {
    this.formData.update((d) => ({ ...d, ...patch }));
  });
  prefillFromRebook = jest.fn(() => [] as string[]);
  applyAddressSuggestion = jest.fn();
  submitOrder = jest.fn();
  // Read by the component's on-destroy park, which runs on EVERY teardown — so a double without it
  // fails every test in this file at cleanup, on a message that names neither the signal nor the
  // hook ("1 component threw errors during cleanup").
  orderPlaced = signal(false);
}

describe('OrderWizardComponent (a11y)', () => {
  let fixture: ComponentFixture<OrderWizardComponent>;
  let facade: FakeOrderWizardFacade;
  let el: HTMLElement;

  async function setup(beforeCreate?: () => void): Promise<void> {
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

    // Anything that has to exist BEFORE ngOnInit runs — a basket already parked in
    // storage, for instance.
    beforeCreate?.();

    fixture = TestBed.createComponent(OrderWizardComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  }

  describe('stepper (AC1, AC2)', () => {
    it('renders completed stepper items as focusable native buttons', async () => {
      await setup();
      facade.activeStep.set(2);
      fixture.detectChanges();

      const items = el.querySelectorAll('.cl-wiz__step');
      const completed = items[0] as HTMLElement;
      expect(completed.tagName).toBe('BUTTON');
      expect(completed.hasAttribute('disabled')).toBe(false);
    });

    it('marks the active stepper item with aria-current="step"', async () => {
      await setup();
      facade.activeStep.set(1);
      fixture.detectChanges();

      const items = el.querySelectorAll('.cl-wiz__step');
      expect(items[1].getAttribute('aria-current')).toBe('step');
      expect(items[0].getAttribute('aria-current')).toBeNull();
    });

    it('navigates to a completed step via keyboard activation (native button = Enter+Space)', async () => {
      await setup();
      facade.activeStep.set(2);
      fixture.detectChanges();

      const completed = el.querySelector('.cl-wiz__step') as HTMLButtonElement;
      completed.click();
      expect(facade.goToStep).toHaveBeenCalledWith(0);
    });
  });

  describe('selection cards (AC1, AC2)', () => {
    it('renders service cards as focusable buttons with aria-pressed reflecting selection', async () => {
      await setup();
      facade.services.set([makeService('s-1', 'Deep clean')]);
      fixture.detectChanges();

      // The artboard lists services as priced rows with their own Add control,
      // so the pressable element is that control rather than the whole row.
      const card = el.querySelector('.cl-wiz__svc-add') as HTMLElement;
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

      const cards = el.querySelectorAll('.cl-wiz__pack');
      const pkgCard = cards[cards.length - 1] as HTMLElement;
      expect(pkgCard.tagName).toBe('BUTTON');
      expect(pkgCard.getAttribute('aria-pressed')).toBe('false');
    });
  });

  describe('room/bathroom counters (AC5)', () => {
    it('gives every counter button an aria-label', async () => {
      await setup();
      // Counts are a chip per choice now, not a pair of +/- steppers.
      const counterBtns = el.querySelectorAll('.cl-wiz__count-chip');
      expect(counterBtns.length).toBeGreaterThanOrEqual(4);
      counterBtns.forEach((b) => {
        expect(b.getAttribute('aria-label')).toBeTruthy();
      });
    });
  });

  describe('contact step labels + errors (AC3, AC4)', () => {
    it('associates each contact label with its input via for/id', async () => {
      await setup();
      facade.activeStep.set(1);
      fixture.detectChanges();

      // `labels` covers both associations — a wrapping <label> and a for/id
      // pair — so the assertion is that the field HAS an accessible name, not
      // which of the two valid mechanisms gives it one.
      const firstNameInput = el.querySelector<HTMLInputElement>('#wizard-first-name');
      expect(firstNameInput).toBeTruthy();
      expect(firstNameInput!.labels?.length).toBeGreaterThan(0);
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

  describe('saved address rows (AC1, AC2)', () => {
    it('renders saved address rows as focusable buttons with aria-pressed', async () => {
      await setup();
      facade.activeStep.set(1);
      facade.isAuthenticated.set(true);
      facade.savedAddresses.set([makeAddress('a-1', 'Home')]);
      facade.selectedSavedAddressId.set('a-1');
      fixture.detectChanges();

      // Saved addresses are chips on the artboard's address step, not rows.
      const row = el.querySelector('.cl-wiz__saved-chip') as HTMLElement;
      expect(row.tagName).toBe('BUTTON');
      expect(row.getAttribute('aria-pressed')).toBe('true');
    });
  });

  describe('payment cards (AC1, AC2)', () => {
    it('renders payment cards as focusable buttons with aria-pressed reflecting the chosen method', async () => {
      await setup();
      facade.activeStep.set(3);
      facade.formData.update((d) => ({ ...d, paymentType: PaymentType.Card }));
      fixture.detectChanges();

      const cards = el.querySelectorAll('.cl-wiz__pay-card');
      expect(cards.length).toBe(2);
      expect((cards[0] as HTMLElement).tagName).toBe('BUTTON');
      expect(cards[0].getAttribute('aria-pressed')).toBe('true');
      expect(cards[1].getAttribute('aria-pressed')).toBe('false');
    });
  });

  describe('what is still missing', () => {
    it('says nothing until the customer has asked to go on', async () => {
      await setup();
      facade.missingReasonsValue.set(['pages.order.missing.services']);
      fixture.detectChanges();

      // A form that greets you red has told you off for not having filled it in.
      expect(el.querySelector('.cl-wiz__blocked')).toBeNull();
      // And the button is clickable, because clicking it is how you ask.
      const advance = el.querySelector('[data-spec-advance]') as HTMLButtonElement;
      expect(advance.disabled).toBe(false);
    });

    it('names every reason once the attempt is made, and does not advance', async () => {
      await setup();
      facade.missingReasonsValue.set([
        'pages.order.missing.services',
        'pages.order.missing.address',
      ]);
      fixture.detectChanges();

      (el.querySelector('[data-spec-advance]') as HTMLButtonElement).click();
      fixture.detectChanges();

      const items = el.querySelectorAll('.cl-wiz__blocked li');
      expect(items.length).toBe(2);
      expect(facade.nextStep).not.toHaveBeenCalled();
    });

    it('advances, and says nothing, when there is nothing missing', async () => {
      await setup();
      facade.missingReasonsValue.set([]);
      fixture.detectChanges();

      (el.querySelector('[data-spec-advance]') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(el.querySelector('.cl-wiz__blocked')).toBeNull();
      expect(facade.nextStep).toHaveBeenCalled();
    });

    it('marks the contact fields touched so their own errors show too', async () => {
      await setup();
      facade.activeStep.set(1);
      facade.missingReasonsValue.set(['pages.order.missing.first_name']);
      fixture.detectChanges();

      // Blur is the only thing that marks a field touched, so a field never
      // visited has no error — which is exactly the field to point at.
      expect(fixture.componentInstance.isTouched('customerFirstName')).toBe(false);

      (el.querySelector('[data-spec-advance]') as HTMLButtonElement).click();
      fixture.detectChanges();

      for (const field of ['customerFirstName', 'customerLastName', 'customerEmail', 'customerPhone']) {
        expect(fixture.componentInstance.isTouched(field)).toBe(true);
      }
    });

    it('forgets the attempt when the customer goes back', async () => {
      await setup();
      facade.missingReasonsValue.set(['pages.order.missing.services']);
      fixture.detectChanges();
      (el.querySelector('[data-spec-advance]') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(el.querySelector('.cl-wiz__blocked')).toBeTruthy();

      fixture.componentInstance.onPrevStep();
      fixture.detectChanges();

      expect(el.querySelector('.cl-wiz__blocked')).toBeNull();
    });
  });

  describe('running total (AC1)', () => {
    it('keeps the summary in the document and after the step in reading order', async () => {
      await setup();
      // The bottom price bar is gone. It was a second copy of the summary,
      // pinned over the form on the widths where the summary rail already
      // stacks into the column. The guarantee that matters is unchanged: the
      // total is on the page without a toggle, and a screen reader meets it
      // after the choices that produce it rather than before them.
      const panel = el.querySelector('.cl-wiz__panel');
      const summary = el.querySelector('.cl-wiz__summary');
      expect(summary).toBeTruthy();
      expect(el.querySelector('.order-wizard__mobile-price')).toBeNull();
      expect(panel!.compareDocumentPosition(summary!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });
  });

  describe('the promo row', () => {
    /**
     * A promo REPLACES the tier/membership discount only when it is larger — it never stacks. The
     * row said "applied" for both outcomes and never named an amount, so a customer whose code lost
     * to their own Plus discount was told it had been applied and shown no figure either way.
     */
    it('names what a winning code takes off', async () => {
      await setup();
      facade.promoCode.set('SAVE300');
      facade.promoCodeState.set({ kind: 'valid', discount: 300 });
      facade.effectivePromoDiscount.set(300);
      facade.appliedDiscountKind.set('promo');
      fixture.detectChanges();

      expect(fixture.componentInstance.promoOutcome()).toBe('applied');
      expect(fixture.componentInstance.promoSavings()).toContain('300');
    });

    it('says a valid code lost to a bigger discount rather than claiming it applied', async () => {
      await setup();
      facade.promoCode.set('SAVE100');
      facade.promoCodeState.set({ kind: 'valid', discount: 100 });
      facade.effectivePromoDiscount.set(100);
      // The membership discount is larger, so the quote keeps it and the promo changes nothing.
      facade.appliedDiscountKind.set('membership');
      fixture.detectChanges();

      expect(fixture.componentInstance.promoOutcome()).toBe('superseded');
    });

    it('reports no outcome until a code has actually validated', async () => {
      await setup();
      facade.promoCode.set('TYPING');
      facade.promoCodeState.set({ kind: 'validating' });
      fixture.detectChanges();

      expect(fixture.componentInstance.promoOutcome()).toBe('none');
    });

    /**
     * `clearPromoCode` existed on the facade with a comment naming "the row's clear-X button" — a
     * button that was never built, so keeping a code for another order meant restarting the booking.
     */
    it('can take a code back off', async () => {
      await setup();
      facade.promoCode.set('SAVE300');
      facade.promoCodeState.set({ kind: 'valid', discount: 300 });
      facade.appliedDiscountKind.set('promo');
      fixture.detectChanges();

      fixture.componentInstance.removePromo();
      fixture.detectChanges();

      expect(facade.clearPromoCode).toHaveBeenCalled();
      expect(fixture.componentInstance.promoOutcome()).toBe('none');
    });
  });

  describe(`the summary rail's discount line`, () => {
    /**
     * The rail handled `membership` and `tier` and nothing else. A promo discount — the one a
     * customer has just typed in and is watching for — appeared nowhere, and neither did the pair
     * when Plus and a loyalty tier both applied, despite the facade's comment promising exactly
     * that. Both are money off a price the customer is about to agree to.
     */
    function discountRows(): string[] {
      return Array.from(el.querySelectorAll('.cl-wiz__summary-discount')).map((row) =>
        (row.textContent ?? '').replace(/\s+/g, ' ').trim(),
      );
    }

    it('shows what a winning promo took off', async () => {
      await setup();
      facade.promoCode.set('SAVE300');
      facade.effectivePromoDiscount.set(300);
      facade.appliedDiscountKind.set('promo');
      fixture.detectChanges();

      const rows = discountRows();
      expect(rows.length).toBe(1);
      // The key, not the sentence: this suite loads no dictionary, so asserting the rendered
      // Czech would be asserting the translate pipe rather than the branch that chose this row.
      expect(rows[0]).toContain('summary.promo_discount');
      expect(rows[0]).toContain('300');
    });

    it('stacks both labels when Plus and a loyalty tier apply together', async () => {
      await setup();
      facade.membershipDiscount.set(120);
      facade.tierDiscount.set(80);
      facade.appliedDiscountKind.set('combined');
      fixture.detectChanges();

      const rows = discountRows();
      expect(rows.length).toBe(2);
      expect(rows[0]).toContain('summary.membership_discount');
      expect(rows[1]).toContain('summary.tier_discount');
      expect(rows.join(' ')).toContain('120');
      expect(rows.join(' ')).toContain('80');
    });

    it('shows no discount line when nothing came off', async () => {
      await setup();
      facade.appliedDiscountKind.set('none');
      fixture.detectChanges();

      expect(discountRows()).toEqual([]);
    });
  });

  describe('the price before the discount', () => {
    /**
     * Two TOTALS, never subtotal-minus-discount. The express surcharge is computed on the
     * undiscounted subtotal, so the chain does not reconcile on an express order — the pair of
     * server-quoted totals does, whatever the surcharge is doing between them.
     */
    it('strikes through the old price and names the saving', async () => {
      await setup();
      facade.totalPrice.set(2000);
      facade.displayedTotalPrice.set(1700);
      fixture.detectChanges();

      expect(fixture.componentInstance.hasSaving()).toBe(true);
      expect(fixture.componentInstance.priceBeforeDiscount()).toContain('2');
      expect(fixture.componentInstance.savingAmount()).toContain('300');
      expect(el.querySelector('.cl-wiz__total-old')).toBeTruthy();
    });

    it('shows nothing when no discount applies', async () => {
      await setup();
      facade.totalPrice.set(2000);
      facade.displayedTotalPrice.set(2000);
      fixture.detectChanges();

      expect(fixture.componentInstance.hasSaving()).toBe(false);
      expect(el.querySelector('.cl-wiz__total-old')).toBeNull();
    });

    /** A rounding tail is not a discount, and must not put a struck-through price on the rail. */
    it('ignores a sub-haler difference', async () => {
      await setup();
      facade.totalPrice.set(2000);
      facade.displayedTotalPrice.set(1999.999);
      fixture.detectChanges();

      expect(fixture.componentInstance.hasSaving()).toBe(false);
    });

    // Every figure used to go through two module-level CZK formatters, whatever the quote said it
    // was priced in. The label is the quote's own currency now, and a EUR quote must never print Kč.
    it("prints every figure in the quote's own currency, not in crowns", async () => {
      await setup();
      facade.currencyCode.set('EUR');
      facade.totalPrice.set(2000);
      facade.displayedTotalPrice.set(1700);
      fixture.detectChanges();

      expect(fixture.componentInstance.priceBeforeDiscount()).toContain('€');
      expect(fixture.componentInstance.priceBeforeDiscount()).not.toContain('Kč');
      expect(fixture.componentInstance.savingAmount()).toContain('€');
      expect(fixture.componentInstance.formatPrice(12.5)).toMatch(/12[.,]50/);
      expect(el.querySelector('.cl-wiz__total')?.textContent).toContain('€');
    });

    /**
     * The express case the subtotal row was rejected over: the surcharge sits on the undiscounted
     * subtotal, so the saving between the two totals is NOT the discount figure. Both numbers are
     * still the server's, and the pair still reads true.
     */
    it('reports the difference between the two totals, not the discount amount', async () => {
      await setup();
      facade.totalPrice.set(2400);          // 2000 + 20% express
      facade.displayedTotalPrice.set(2040); // (2000 − 300) scaled by the same gross ratio
      facade.effectivePromoDiscount.set(300);
      facade.appliedDiscountKind.set('promo');
      fixture.detectChanges();

      expect(fixture.componentInstance.savingAmount()).toContain('360');
      expect(fixture.componentInstance.savingAmount()).not.toContain('300 ');
    });
  });
  /**
   * A basket that survives leaving the page.
   *
   * The wizard parked a draft in exactly two places — the trip to sign in and the trip to Plus —
   * so every other departure (the logo, a nav link, the back button) emptied it. It now parks on
   * destroy, whatever the way out.
   *
   * The restore had a second defect that only showed once the first was fixed: it ran
   * synchronously in `ngOnInit`, immediately after `initialize()` DISPATCHES the catalogue load.
   * On a cold store it therefore checked the basket against an empty list, dropped every service
   * in it, and announced a "partially restored" draft — an emptier basket than no restore at all.
   */
  describe('a parked basket', () => {
    const parked = {
      savedAt: Date.now(),
      step: 2,
      data: { ...ORDER_WIZARD_INITIAL_DATA, selectedServiceIds: ['s-1'] },
    };

    afterEach(() => sessionStorage.clear());

    it('is not applied against a catalogue that has not arrived yet', async () => {
      await setup(() =>
        sessionStorage.setItem('cleansia_order_draft', JSON.stringify(parked))
      );

      // The store is cold — `facade.services()` is still []. Restoring here is what dropped the
      // basket and then blamed the catalogue for it.
      expect(facade.goToStep).not.toHaveBeenCalled();
      expect(facade.formData().selectedServiceIds).toEqual([]);
    });

    it('is applied once the catalogue lands, on the step it was parked from', async () => {
      await setup(() =>
        sessionStorage.setItem('cleansia_order_draft', JSON.stringify(parked))
      );

      facade.services.set([makeService('s-1', 'Deep clean')]);
      fixture.detectChanges();

      expect(facade.formData().selectedServiceIds).toEqual(['s-1']);
      expect(facade.goToStep).toHaveBeenCalledWith(2);
    });

    it('drops what is no longer on offer rather than restoring a dead price', async () => {
      await setup(() =>
        sessionStorage.setItem('cleansia_order_draft', JSON.stringify(parked))
      );

      facade.services.set([makeService('s-9', 'Something else')]);
      fixture.detectChanges();

      expect(facade.formData().selectedServiceIds).toEqual([]);
      expect(TestBed.inject(SnackbarService).showError).toHaveBeenCalled();
    });

    it('parks on the way out, not only on the two trips that were instrumented', async () => {
      await setup();
      facade.updateFormData({ selectedServiceIds: ['s-1'] });

      fixture.destroy();

      expect(sessionStorage.getItem('cleansia_order_draft')).not.toBeNull();
    });

    it('does not park a basket that has already been bought', async () => {
      await setup();
      facade.updateFormData({ selectedServiceIds: ['s-1'] });
      facade.orderPlaced.set(true);

      fixture.destroy();

      expect(sessionStorage.getItem('cleansia_order_draft')).toBeNull();
    });

    it('does not park an empty basket, which would offer nothing back', async () => {
      await setup();

      fixture.destroy();

      expect(sessionStorage.getItem('cleansia_order_draft')).toBeNull();
    });
  });
});
