import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { By } from '@angular/platform-browser';
import { TranslateModule } from '@ngx-translate/core';
import { computed, signal } from '@angular/core';
import { MembershipPlanDetailDto } from '@cleansia/admin-services';
import { Subject } from 'rxjs';
import { MembershipPlanFormComponent } from './membership-plan-form.component';
import { MembershipPlanFormFacade } from './membership-plan-form.facade';
import {
  PRICE_BLOCK_HALF_FILLED_ERROR,
  PlanCurrencyOption,
} from './membership-plan-form.models';

const CZK: PlanCurrencyOption = {
  code: 'CZK',
  symbol: 'Kč',
  name: 'Czech koruna',
  isDefault: true,
  isActive: true,
};
const EUR: PlanCurrencyOption = {
  code: 'EUR',
  symbol: '€',
  name: 'Euro',
  isDefault: false,
  isActive: false,
};

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly plan = signal<MembershipPlanDetailDto | null>(null);
  readonly currencies = signal<PlanCurrencyOption[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly benefitsLocked = computed(() => this.plan()?.benefitsLocked === true);
  loadPlan = jest.fn();
  loadCurrencies = jest.fn();
  create = jest.fn();
  update = jest.fn();
  navigateBack = jest.fn();
}

describe('MembershipPlanFormComponent', () => {
  let fixture: ComponentFixture<MembershipPlanFormComponent>;
  let component: MembershipPlanFormComponent;
  let facade: FacadeStub;
  let routeData: Record<string, unknown>;
  let routeParams: Record<string, string>;

  function fillValidPlan(): void {
    component.form.patchValue({
      code: 'PLUS_MONTHLY',
      name: 'Cleansia Plus',
    });
  }

  function priceBlock(code: string) {
    return component.form.controls.prices.get(code);
  }

  function inputFor(controlName: string): HTMLInputElement {
    return fixture.debugElement.query(By.css(`[formControlName="${controlName}"] input`))
      .nativeElement as HTMLInputElement;
  }

  function acceptsTyping(controlName: string): boolean {
    const input = inputFor(controlName);
    return !input.readOnly && !input.disabled;
  }

  async function setup(mode: 'create' | 'edit'): Promise<void> {
    facade = new FacadeStub();
    routeData = { mode };
    routeParams = mode === 'edit' ? { id: 'plan-1' } : {};

    await TestBed.configureTestingModule({
      imports: [MembershipPlanFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: routeData,
              paramMap: { get: (key: string) => routeParams[key] ?? null },
            },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(MembershipPlanFormComponent, {
        add: {
          providers: [{ provide: MembershipPlanFormFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(MembershipPlanFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  describe('create mode', () => {
    beforeEach(async () => {
      await setup('create');
    });

    it('uses OnPush change detection', () => {
      const meta = (
        MembershipPlanFormComponent as unknown as { ɵcmp: { onPush: boolean } }
      ).ɵcmp;
      expect(meta.onPush).toBe(true);
    });

    it('asks the facade for the currency list on init', () => {
      expect(facade.loadCurrencies).toHaveBeenCalledTimes(1);
    });

    it('renders one price block per currency, active and inactive alike, each with a price and a Stripe id control', () => {
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();

      const blocks = fixture.debugElement.queryAll(By.css('.currency-price-block'));
      expect(blocks.length).toBe(2);
      expect(priceBlock('CZK')?.get('price')).toBeTruthy();
      expect(priceBlock('CZK')?.get('stripePriceId')).toBeTruthy();
      expect(priceBlock('EUR')?.get('price')).toBeTruthy();
      expect(priceBlock('EUR')?.get('stripePriceId')).toBeTruthy();
      const badges = fixture.debugElement.queryAll(By.css('.currency-price-block__badge'));
      expect(badges[0].nativeElement.classList).not.toContain('currency-price-block__badge--optional');
      expect(badges[1].nativeElement.classList).toContain('currency-price-block__badge--optional');
    });

    it('renders an input bound to the express-upgrade quota', () => {
      const quotaInput = fixture.debugElement.query(
        By.css('[formControlName="expressUpgradesPerMonth"]')
      );

      expect(quotaInput).toBeTruthy();
    });

    it('sends only the filled price block — a blank block is absent, not zero', () => {
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();
      fillValidPlan();
      priceBlock('CZK')?.patchValue({ price: '199', stripePriceId: 'price_czk' });

      component.onSave();

      expect(facade.create).toHaveBeenCalledWith(
        expect.objectContaining({
          prices: { CZK: { price: 199, stripePriceId: 'price_czk' } },
        })
      );
    });

    it('saves a plan with no price block filled', () => {
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();
      fillValidPlan();

      component.onSave();

      expect(facade.create).toHaveBeenCalledWith(
        expect.objectContaining({ prices: {} })
      );
    });

    it('refuses a half-filled block and sends nothing', () => {
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();
      fillValidPlan();
      priceBlock('EUR')?.patchValue({ price: '9.9', stripePriceId: '' });

      component.onSave();

      expect(facade.create).not.toHaveBeenCalled();
      expect(priceBlock('EUR')?.errors?.[PRICE_BLOCK_HALF_FILLED_ERROR]).toBe(true);
      expect(component.form.invalid).toBe(true);
    });

    it('sends the express quota an admin set on a new plan', () => {
      fillValidPlan();
      component.form.controls.expressUpgradesPerMonth.setValue(4);

      component.onSave();

      expect(facade.create).toHaveBeenCalledWith(
        expect.objectContaining({ expressUpgradesPerMonth: 4 })
      );
    });

    it('refuses to save a negative express quota', () => {
      fillValidPlan();
      component.form.controls.expressUpgradesPerMonth.setValue(-1);

      component.onSave();

      expect(facade.create).not.toHaveBeenCalled();
      expect(component.form.controls.expressUpgradesPerMonth.errors?.['min']).toBeTruthy();
    });

    it('renders an input bound to the free-trial length', () => {
      const trialInput = fixture.debugElement.query(
        By.css('[formControlName="trialPeriodDays"]')
      );

      expect(trialInput).toBeTruthy();
    });

    it('offers a new plan a 14-day trial and sends it', () => {
      fillValidPlan();

      component.onSave();

      expect(facade.create).toHaveBeenCalledWith(
        expect.objectContaining({ trialPeriodDays: 14 })
      );
    });

    it('sends a zero trial when the admin turns it off', () => {
      fillValidPlan();
      component.form.controls.trialPeriodDays.setValue(0);

      component.onSave();

      expect(facade.create).toHaveBeenCalledWith(
        expect.objectContaining({ trialPeriodDays: 0 })
      );
    });

    it('refuses to save a negative trial', () => {
      fillValidPlan();
      component.form.controls.trialPeriodDays.setValue(-1);

      component.onSave();

      expect(facade.create).not.toHaveBeenCalled();
      expect(component.form.controls.trialPeriodDays.errors?.['min']).toBeTruthy();
    });
  });

  describe('edit mode', () => {
    const detail = MembershipPlanDetailDto.fromJS({
      id: 'plan-1',
      code: 'PLUS_MONTHLY',
      name: 'Cleansia Plus',
      billingInterval: 1,
      prices: {
        CZK: { price: 199, monthlyEquivalentPrice: 199, stripePriceId: 'price_czk' },
      },
      discountPercentage: 10,
      trialPeriodDays: 30,
      allowsExpressUpgrade: true,
      expressUpgradesPerMonth: 2,
      isActive: true,
    });

    beforeEach(async () => {
      await setup('edit');
    });

    it('loads the plan and the currencies', () => {
      expect(facade.loadPlan).toHaveBeenCalledWith('plan-1');
      expect(facade.loadCurrencies).toHaveBeenCalledTimes(1);
    });

    it('populates the priced block and leaves the unpriced one blank, not zero', () => {
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();
      facade.plan.set(detail);
      fixture.detectChanges();

      expect(priceBlock('CZK')?.value).toEqual({ price: 199, stripePriceId: 'price_czk' });
      expect(priceBlock('EUR')?.value).toEqual({ price: null, stripePriceId: '' });
    });

    it('replays the plan prices when the currency list lands after the plan', () => {
      facade.plan.set(detail);
      fixture.detectChanges();
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();

      expect(priceBlock('CZK')?.value).toEqual({ price: 199, stripePriceId: 'price_czk' });
      expect(priceBlock('EUR')?.value).toEqual({ price: null, stripePriceId: '' });
    });

    it('updates with the plan id and only the filled blocks', () => {
      facade.currencies.set([CZK, EUR]);
      fixture.detectChanges();
      facade.plan.set(detail);
      fixture.detectChanges();

      component.onSave();

      expect(facade.update).toHaveBeenCalledWith(
        'plan-1',
        expect.objectContaining({
          name: 'Cleansia Plus',
          prices: { CZK: { price: 199, stripePriceId: 'price_czk' } },
        })
      );
    });

    it("keeps a subscribed plan's discount and express quota as they are, says why, and sends them back unchanged", () => {
      facade.plan.set(MembershipPlanDetailDto.fromJS({ ...detail.toJSON(), benefitsLocked: true }));
      fixture.detectChanges();

      expect(component.form.controls.discountPercentage.disabled).toBe(true);
      expect(component.form.controls.expressUpgradesPerMonth.disabled).toBe(true);
      expect(component.form.controls.allowsExpressUpgrade.disabled).toBe(false);
      // A disabled control alone does not reach the shared input's DOM element.
      expect(acceptsTyping('discountPercentage')).toBe(false);
      expect(acceptsTyping('expressUpgradesPerMonth')).toBe(false);
      expect(acceptsTyping('trialPeriodDays')).toBe(true);
      const hint = fixture.debugElement.query(By.css('[data-spec-benefits-locked]'));
      expect(hint.nativeElement.textContent).toContain(
        'pages.membership_plans.form.field.benefits_locked_help'
      );

      component.onSave();

      expect(facade.update).toHaveBeenCalledWith(
        'plan-1',
        expect.objectContaining({ discountPercentage: 10, expressUpgradesPerMonth: 2 })
      );
    });

    it('leaves the discount and express quota editable on a plan nobody has subscribed to', () => {
      facade.plan.set(MembershipPlanDetailDto.fromJS({ ...detail.toJSON(), benefitsLocked: false }));
      fixture.detectChanges();

      expect(component.form.controls.discountPercentage.enabled).toBe(true);
      expect(component.form.controls.expressUpgradesPerMonth.enabled).toBe(true);
      expect(acceptsTyping('discountPercentage')).toBe(true);
      expect(acceptsTyping('expressUpgradesPerMonth')).toBe(true);
      expect(fixture.debugElement.query(By.css('[data-spec-benefits-locked]'))).toBeNull();
    });

    it('shows the trial length the plan carries, not the new-plan default, and sends it back', () => {
      facade.plan.set(detail);
      fixture.detectChanges();

      expect(component.form.controls.trialPeriodDays.value).toBe(30);

      component.onSave();

      expect(facade.update).toHaveBeenCalledWith(
        'plan-1',
        expect.objectContaining({ trialPeriodDays: 30 })
      );
    });
  });
});
