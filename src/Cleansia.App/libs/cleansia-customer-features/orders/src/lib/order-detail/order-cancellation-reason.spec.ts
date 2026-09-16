import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  GetMyMembershipResponse,
  OrderItem,
  OrderStatus,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { Subject } from 'rxjs';
import { OrderDetailComponent } from './order-detail.component';
import { OrderMarketFacade } from '../order-market.facade';
import { OrderDetailFacade } from './order-detail.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../apps/cleansia.app/src/assets/i18n');

/** Mirrors `Cleansia.Core.Domain.Orders.OrderCancellationReasons` — every key the server writes. */
const PLATFORM_REASONS = [
  'payment_not_completed',
  'recurring_not_confirmed',
  'company_wind_down',
  'no_cleaner_available',
] as const;

/**
 * The unfilled-order sweep refunds the booking and pays the apology credit, and each of those is a
 * line of its own on the detail. The reason sentence says only why; a money claim inside it would
 * be a second statement of the same fact that can drift from the first.
 */
const MONEY_WORDS: Record<string, readonly string[]> = {
  en: ['refund', 'credit', 'paid'],
  cs: ['vrac', 'vrát', 'kredit', 'zaplat'],
  sk: ['vrac', 'vrát', 'kredit', 'zaplat'],
  uk: ['поверн', 'кредит', 'бонус', 'сплат'],
  ru: ['возвр', 'верн', 'кредит', 'бонус', 'оплат'],
};

function reasonCopy(locale: string): Record<string, string> {
  const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as {
    pages?: { order_detail?: { cancellation_reason?: Record<string, string> } };
  };
  const copy = bundle.pages?.order_detail?.cancellation_reason;
  if (!copy) throw new Error(`pages.order_detail.cancellation_reason missing from ${locale}.json`);
  return copy;
}

describe('order detail cancellation reason', () => {
  describe('the reason map', () => {
    let getById: Subject<OrderItem>;
    let component: OrderDetailComponent;

    function cancelledOrder(systemCancellationReason: string | undefined): OrderItem {
      return OrderItem.fromJS({
        id: 'ord-1',
        orderStatus: { value: OrderStatus.Cancelled, name: 'Cancelled' },
        cleaningDateTime: '2026-09-01T08:00:00Z',
        systemCancellationReason,
      });
    }

    function reasonKeyFor(systemCancellationReason: string | undefined): string | null {
      getById.next(cancelledOrder(systemCancellationReason));
      return component['cancellationReasonKey']();
    }

    beforeEach(() => {
      getById = new Subject<OrderItem>();

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          OrderDetailComponent,
          OrderDetailFacade,
          { provide: OrderMarketFacade, useValue: { label: jest.fn() } },
          OrderPreferredOfferFacade,
          { provide: PLATFORM_ID, useValue: 'browser' },
          {
            provide: CustomerClient,
            useValue: {
              orderClient: { getById: () => getById.asObservable() },
              membershipClient: {
                getMine: () => new Subject<GetMyMembershipResponse>().asObservable(),
              },
            },
          },
          { provide: CustomerAuthService, useValue: { isLoggedIn: () => true } },
          {
            provide: SnackbarService,
            useValue: { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() },
          },
          {
            provide: TranslateService,
            useValue: { instant: (key: string) => key, currentLang: 'en' },
          },
          { provide: Router, useValue: { navigate: jest.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ord-1' } } } },
        ],
      });

      component = TestBed.inject(OrderDetailComponent);
      component.ngOnInit();
    });

    it.each(PLATFORM_REASONS)('turns order.cancelled.%s into its translation key', (reason) => {
      expect(reasonKeyFor(`order.cancelled.${reason}`)).toBe(
        `pages.order_detail.cancellation_reason.${reason}`
      );
    });

    it('says why when the unfilled-order sweep cancelled the booking', () => {
      expect(reasonKeyFor('order.cancelled.no_cleaner_available')).toBe(
        'pages.order_detail.cancellation_reason.no_cleaner_available'
      );
    });

    it('says nothing for a reason this build has never heard of, and for none at all', () => {
      expect(reasonKeyFor('order.cancelled.something_newer')).toBeNull();
      expect(reasonKeyFor(undefined)).toBeNull();
    });
  });

  describe.each(LOCALES)('the copy in %s', (locale) => {
    const copy = reasonCopy(locale);

    it('ships a sentence for every platform reason', () => {
      for (const reason of PLATFORM_REASONS) {
        expect(copy[reason]).toBeTruthy();
      }
    });

    it('leaves the refund and the apology credit to their own lines', () => {
      const sentence = copy['no_cleaner_available'].toLowerCase();
      for (const word of MONEY_WORDS[locale]) {
        expect(sentence).not.toContain(word);
      }
    });
  });

  it('translates the no-cleaner sentence rather than copying the English over', () => {
    const english = reasonCopy('en')['no_cleaner_available'];
    for (const locale of LOCALES.slice(1)) {
      expect(reasonCopy(locale)['no_cleaner_available']).not.toBe(english);
    }
  });
});
