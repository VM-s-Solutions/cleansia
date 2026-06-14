import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  PagedDataOfPromoCodeListItem,
  PromoCodeListItem,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PromoCodesListFacade } from './promo-codes-list.facade';

describe('PromoCodesListFacade', () => {
  let facade: PromoCodesListFacade;
  let promoCodeClient: { getPaged: jest.Mock; deactivate: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showApiError: jest.Mock;
  };

  const item = PromoCodeListItem.fromJS({ id: 'promo-1', code: 'SPRING' });
  const page = PagedDataOfPromoCodeListItem.fromJS({ data: [item], total: 1 });

  beforeEach(() => {
    promoCodeClient = { getPaged: jest.fn(), deactivate: jest.fn() };
    snackbar = { showSuccess: jest.fn(), showApiError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        PromoCodesListFacade,
        { provide: AdminClient, useValue: { adminPromoCodeClient: promoCodeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(PromoCodesListFacade);
  });

  it('deactivates a promo code, shows success and reloads on success', () => {
    promoCodeClient.deactivate.mockReturnValue(of({}));
    promoCodeClient.getPaged.mockReturnValue(of(page));

    facade.deactivate(item);

    expect(promoCodeClient.deactivate).toHaveBeenCalledWith('promo-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
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
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
    expect(promoCodeClient.getPaged).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });

  it('does nothing for a promo code without an id', () => {
    facade.deactivate(PromoCodeListItem.fromJS({ code: 'NOID' }));

    expect(promoCodeClient.deactivate).not.toHaveBeenCalled();
  });
});
