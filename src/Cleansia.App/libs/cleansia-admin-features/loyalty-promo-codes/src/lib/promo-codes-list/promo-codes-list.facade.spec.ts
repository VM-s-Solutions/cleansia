import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  PagedDataOfPromoCodeListItem,
  PromoCodeListItem,
} from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { PromoCodesListFacade } from './promo-codes-list.facade';

describe('PromoCodesListFacade', () => {
  let facade: PromoCodesListFacade;
  let promoCodeClient: { getPaged: jest.Mock; deactivate: jest.Mock };
  let confirmMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock; showSuccessTranslated: jest.Mock;
    showApiError: jest.Mock;
  };

  const item = PromoCodeListItem.fromJS({ id: 'promo-1', code: 'SPRING' });
  const page = PagedDataOfPromoCodeListItem.fromJS({ data: [item], total: 1 });

  beforeEach(() => {
    promoCodeClient = { getPaged: jest.fn(), deactivate: jest.fn() };
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        PromoCodesListFacade,
        { provide: AdminClient, useValue: { adminPromoCodeClient: promoCodeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs', onLangChange: EMPTY },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(PromoCodesListFacade);
  });

  describe('filters', () => {
    beforeEach(() => {
      jest.useFakeTimers();
      promoCodeClient.getPaged.mockReturnValue(of(page));
    });

    afterEach(() => jest.useRealTimers());

    it('sends the trimmed code search once it settles and names it in one chip', () => {
      facade.filterForm.patchValue({ searchCode: '  spring ' });
      jest.advanceTimersByTime(500);

      const args = promoCodeClient.getPaged.mock.calls.at(-1);
      expect(args?.[0]).toBeUndefined();
      expect(args?.[1]).toBeUndefined();
      expect(args?.[2]).toBe('spring');
      expect(args?.[3]).toBe(0);
      expect(facade.filters.chips()).toEqual([
        { key: 'searchCode', label: 'pages.promo_codes.filters.search', value: 'spring' },
      ]);
    });

    it('maps the status choice onto the active and expired flags and names the status in its chip', () => {
      facade.filterForm.patchValue({ status: 'expired' });
      jest.advanceTimersByTime(500);

      const args = promoCodeClient.getPaged.mock.calls.at(-1);
      expect(args?.[0]).toBeUndefined();
      expect(args?.[1]).toBe(true);
      expect(facade.filters.chips()).toEqual([
        {
          key: 'status',
          label: 'pages.promo_codes.filters.status',
          value: 'pages.promo_codes.status_filter_expired',
        },
      ]);

      facade.filterForm.patchValue({ status: 'inactive' });
      jest.advanceTimersByTime(500);
      expect(promoCodeClient.getPaged.mock.calls.at(-1)?.[0]).toBe(false);
    });

    it('starts on the first page again when a filter changes, and reset clears every chip', () => {
      facade.onPageChange(40, 20);
      facade.filterForm.patchValue({ status: 'active' });
      jest.advanceTimersByTime(500);
      expect(promoCodeClient.getPaged.mock.calls.at(-1)?.[3]).toBe(0);

      facade.filters.reset();
      expect(facade.filters.chips()).toEqual([]);
      const args = promoCodeClient.getPaged.mock.calls.at(-1);
      expect(args?.[0]).toBeUndefined();
      expect(args?.[1]).toBeUndefined();
      expect(args?.[2]).toBeUndefined();
    });
  });

  it('deactivates a promo code, shows success and reloads on success', () => {
    promoCodeClient.deactivate.mockReturnValue(of({}));
    promoCodeClient.getPaged.mockReturnValue(of(page));

    facade.deactivate(item);

    expect(promoCodeClient.deactivate).toHaveBeenCalledWith('promo-1');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.promo_codes.form.success.deactivated'
    );
    expect(promoCodeClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.loading()).toBe(false);
  });

  it('surfaces the error and resets loading on deactivate failure', () => {
    const error = { result: { detail: 'promo_code.in_use' } };
    promoCodeClient.deactivate.mockReturnValue(throwError(() => error));

    facade.deactivate(item);

    expect(snackbar.showApiError).toHaveBeenCalledWith(error);
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(promoCodeClient.getPaged).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });

  it('does nothing for a promo code without an id', () => {
    facade.deactivate(PromoCodeListItem.fromJS({ code: 'NOID' }));

    expect(promoCodeClient.deactivate).not.toHaveBeenCalled();
  });

  it('does nothing when the confirmation is declined', () => {
    confirmMock.mockReturnValue(of(false));

    facade.deactivate(item);

    expect(confirmMock).toHaveBeenCalledWith(
      'pages.promo_codes.detail.deactivate_confirm_body',
      'pages.promo_codes.detail.deactivate_confirm_title',
      undefined,
      { acceptLabelKey: 'pages.promo_codes.detail.deactivate_confirm_yes' }
    );
    expect(promoCodeClient.deactivate).not.toHaveBeenCalled();
    expect(promoCodeClient.getPaged).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });
});
