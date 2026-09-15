import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { SnackbarService } from '@cleansia/services';
import {
  GetLoyaltyTiersTierInfo,
  GetMyLoyaltyResponse,
  GetMyReferralResponse,
} from '@cleansia/customer-services';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { RewardsComponent } from './rewards.component';
import { RewardsFacade } from './rewards.facade';

/**
 * The tier discount, as the page prints it.
 *
 * `LoyaltyTierConfig.DiscountPercent` is a FRACTION despite its name — `UpdateTierConfig`
 * validates it as `0 <= p <= 1`, and `LoyaltyService` spends it as `orderTotal * DiscountPercent`
 * with no division. The seeded tiers are therefore 0.05 / 0.1 / 0.12.
 *
 * The component already had the converter. The two places that actually PRINT the number did not
 * go through it, so the page read "Silver Mopper 0.05%" — a fiftieth of the real discount. Both
 * mobile apps multiply by 100 (`RewardsTab.kt:652`, `LoyaltyPresentation.swift:44`), so the web was
 * the one surface out of three that got it wrong.
 *
 * These assert the RENDERED text, not the helper: a green unit test on `percentOf` is exactly what
 * this repo had, and it did not stop a template from bypassing it.
 */
describe('RewardsComponent — the discount a tier prints', () => {
  let fixture: ComponentFixture<RewardsComponent>;

  const DICTIONARY = {
    pages: {
      rewards: {
        discount_min_order: '{{percent}}% off orders over {{minAmount}}',
        discount_basic: '{{percent}}% off every order',
        no_discount_yet: 'No discount yet',
        discount_of: '{{percent}}% off',
        points_threshold: '{{count}} points',
        your_tier: 'Your tier',
        tier: { '2': 'Silver Mopper', '3': 'Gold Polisher', '4': 'Platinum Sparkler' },
      },
    },
  };

  // The seeded three, as the API really sends them.
  const tiersWithFractions = [
    { tier: 2, lifetimePointsThreshold: 100, discountPercent: 0.05 },
    { tier: 3, lifetimePointsThreshold: 300, discountPercent: 0.1 },
    { tier: 4, lifetimePointsThreshold: 600, discountPercent: 0.12 },
  ].map((t) => GetLoyaltyTiersTierInfo.fromJS(t));

  async function setup(
    currentDiscountPercent: number,
    options: {
      tiers?: GetLoyaltyTiersTierInfo[];
      defaultCurrencyCode?: string | null;
      floorApplies?: boolean;
    } = {},
  ): Promise<void> {
    const facade = {
      loadAll: jest.fn(),
      defaultCurrencyCode: signal<string | null>(options.defaultCurrencyCode ?? null),
      floorApplies: signal(options.floorApplies ?? true),
      account: signal(
        GetMyLoyaltyResponse.fromJS({
          currentTier: 2,
          lifetimePoints: 150,
          points: 150,
          currentDiscountPercent,
        }),
      ),
      tiers: signal(options.tiers ?? tiersWithFractions),
      recentActivity: signal([]),
      referralAccount: signal(GetMyReferralResponse.fromJS({ code: 'ABC123' })),
      loading: signal(false),
      error: signal<string | null>(null),
      hasLoaded: signal(true),
      tierKey: (t: unknown) => String(t),
    };

    await TestBed.configureTestingModule({
      imports: [
        RewardsComponent,
        // A real dictionary, because the assertion is about the INTERPOLATED number. With no
        // loader ngx-translate echoes the bare key and silently drops the params, so a test
        // written against the rendered text passes on any value at all — including 0.05.
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: { getTranslation: () => of(DICTIONARY) } },
        }),
      ],
      providers: [
        provideHttpClient(),
        provideNoopAnimations(),
        { provide: SnackbarService, useValue: { showError: jest.fn(), showSuccess: jest.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      ],
    })
      .overrideComponent(RewardsComponent, {
        set: { providers: [{ provide: RewardsFacade, useValue: facade }] },
      })
      .compileComponents();

    TestBed.inject(TranslateService).use('en');
    fixture = TestBed.createComponent(RewardsComponent);
    fixture.detectChanges();
  }

  it('prints a whole number of percent, not the fraction the API sends', async () => {
    await setup(0.05);

    // `innerText` is not implemented in jsdom — `textContent` is. No dictionary is loaded, so
    // ngx-translate echoes the key and the INTERPOLATED value is what is being asserted, which is
    // exactly what the customer reads beside the % sign.
    const cards = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cl-rwd__tier-discount'),
    )
      .map((el) => el.textContent ?? '')
      .join(' | ');

    expect(cards).toContain('5');
    expect(cards).toContain('10');
    expect(cards).toContain('12');
    // The defect, stated as the thing that must never come back.
    expect(cards).not.toContain('0.05');
    expect(cards).not.toContain('0.1%');
    expect(cards).not.toContain('0.12');
  });

  const tierDiscountLines = (): string =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cl-rwd__tier-discount'),
    )
      .map((el) => el.textContent ?? '')
      .join(' | ');

  // The floor is `LoyaltyTierConfig.MinimumOrderAmountForDiscount`, a platform-default-currency
  // number the copy used to restate as "over 1000 CZK" in every perk label. It is printed ONCE
  // now, from the tier the server sent, in the default currency's code — never in crowns by name.
  it('prints the floor from the tier, labelled with the platform default currency', async () => {
    await setup(0.05, {
      defaultCurrencyCode: 'EUR',
      tiers: [
        GetLoyaltyTiersTierInfo.fromJS({
          tier: 2,
          lifetimePointsThreshold: 100,
          discountPercent: 0.05,
          minimumOrderAmountForDiscount: 1000,
        }),
      ],
    });

    const lines = tierDiscountLines();
    expect(lines).toContain('5% off orders over');
    expect(lines).toContain('€');
    expect(lines).toContain('1,000');
    expect(lines).not.toContain('Kč');
    expect(lines).not.toContain('CZK');
  });

  // In a market priced in another currency no floor applies (ADR-0058 D5), and a number in a
  // unit it was never set in must not be printed.
  it('omits the floor line in a market the floor does not apply to', async () => {
    await setup(0.05, {
      defaultCurrencyCode: 'CZK',
      floorApplies: false,
      tiers: [
        GetLoyaltyTiersTierInfo.fromJS({
          tier: 2,
          lifetimePointsThreshold: 100,
          discountPercent: 0.05,
          minimumOrderAmountForDiscount: 1000,
        }),
      ],
    });

    const lines = tierDiscountLines();
    expect(lines).not.toContain('over');
    expect(lines).not.toContain('1,000');
    expect(lines).toContain('5%');
  });

  it('states no floor for a tier that has none, and no discount for a tier without one', async () => {
    await setup(0.05, {
      tiers: [
        GetLoyaltyTiersTierInfo.fromJS({ tier: 2, lifetimePointsThreshold: 100, discountPercent: 0.05 }),
        GetLoyaltyTiersTierInfo.fromJS({ tier: 1, lifetimePointsThreshold: 0, discountPercent: 0 }),
      ],
    });

    const lines = tierDiscountLines();
    expect(lines).toContain('5% off every order');
    expect(lines).toContain('No discount yet');
    expect(lines).not.toContain('over');
  });

  it('converts every seeded tier: 0.05 / 0.1 / 0.12 read as 5 / 10 / 12', async () => {
    await setup(0.05);
    const component = fixture.componentInstance;

    expect(component.percentOf(0.05)).toBe(5);
    expect(component.percentOf(0.1)).toBe(10);
    expect(component.percentOf(0.12)).toBe(12);
  });

  it('leaves a value already expressed in percent alone', async () => {
    await setup(0.05);
    const component = fixture.componentInstance;

    // The guard, not a hedge: if the API's units ever move to whole percent, 12 must stay 12
    // rather than silently becoming 1200.
    expect(component.percentOf(12)).toBe(12);
  });

  it('treats no discount as zero rather than NaN', async () => {
    await setup(0);
    const component = fixture.componentInstance;

    expect(component.percentOf(0)).toBe(0);
    expect(component.percentOf(undefined)).toBe(0);
  });
});
