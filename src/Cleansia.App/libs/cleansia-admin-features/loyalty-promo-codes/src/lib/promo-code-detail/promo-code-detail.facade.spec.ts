import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, PromoCodeDetailDto } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { of } from 'rxjs';
import { PromoCodeDetailFacade } from './promo-code-detail.facade';

describe('PromoCodeDetailFacade', () => {
  let facade: PromoCodeDetailFacade;
  let detailsMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };
  let router: { navigate: jest.Mock };

  const detail = PromoCodeDetailDto.fromJS({ id: 'promo-1', code: 'SPRING', isActive: true });

  beforeEach(() => {
    TestBed.resetTestingModule();
    detailsMock = jest.fn().mockReturnValue(of(detail));
    deactivateMock = jest.fn().mockReturnValue(of({ promoCodeId: 'promo-1' }));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        PromoCodeDetailFacade,
        { provide: AdminClient, useValue: { adminPromoCodeClient: { details: detailsMock, deactivate: deactivateMock } } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        { provide: Router, useValue: router },
      ],
    });

    facade = TestBed.inject(PromoCodeDetailFacade);
    facade.loadPromoCode('promo-1');
  });

  it('holds the loaded promo code', () => {
    expect(facade.promoCode()).toBe(detail);
    expect(facade.loading()).toBe(false);
  });

  it('leaves for the list when the read answers nothing', () => {
    detailsMock.mockReturnValue(of(null));

    facade.loadPromoCode('promo-gone');

    expect(router.navigate).toHaveBeenCalledWith(['/loyalty/promos']);
  });

  describe('deactivate', () => {
    it('asks, deactivates, toasts and re-reads the promo code', () => {
      facade.deactivate();

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.promo_codes.detail.deactivate_confirm_body',
        'pages.promo_codes.detail.deactivate_confirm_title',
        undefined,
        { acceptLabelKey: 'pages.promo_codes.detail.deactivate_confirm_yes' }
      );
      expect(deactivateMock).toHaveBeenCalledWith('promo-1');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.promo_codes.form.success.deactivated');
      expect(detailsMock).toHaveBeenCalledTimes(2);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.deactivate();

      expect(deactivateMock).not.toHaveBeenCalled();
      expect(detailsMock).toHaveBeenCalledTimes(1);
    });
  });
});
