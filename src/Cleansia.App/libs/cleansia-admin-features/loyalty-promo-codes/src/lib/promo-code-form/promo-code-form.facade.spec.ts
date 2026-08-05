import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreatePromoCodeCommand,
  PromoCodeType,
  UpdatePromoCodeCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import {
  PromoCodeCreateInput,
  PromoCodeFormFacade,
  PromoCodeUpdateInput,
} from './promo-code-form.facade';

const VALID_FROM = new Date('2026-01-01T00:00:00.000Z');
const VALID_UNTIL = new Date('2026-03-01T00:00:00.000Z');

describe('PromoCodeFormFacade', () => {
  let facade: PromoCodeFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let navigate: jest.Mock;

  const percentInput: PromoCodeCreateInput = {
    code: 'SPRING20',
    type: PromoCodeType.PercentDiscount,
    discountPercentUi: 20,
    minimumOrderAmount: 500,
    maxRedemptionsPerUser: 1,
    globalMaxRedemptions: 1000,
    validFrom: VALID_FROM,
    validUntil: VALID_UNTIL,
    description: 'Spring campaign',
  };

  const updateInput: PromoCodeUpdateInput = {
    isActive: false,
    validFrom: VALID_FROM,
    validUntil: VALID_UNTIL,
    minimumOrderAmount: 500,
    maxRedemptionsPerUser: 2,
    globalMaxRedemptions: 1000,
    description: 'Paused',
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ promoCodeId: 'promo-1' }));
    updateMock = jest.fn().mockReturnValue(of({ promoCodeId: 'promo-1' }));
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        PromoCodeFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPromoCodeClient: {
              create: createMock,
              update: updateMock,
              details: jest.fn().mockReturnValue(of(null)),
            },
            adminCurrencyClient: { getOverview: jest.fn().mockReturnValue(of([])) },
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(PromoCodeFormFacade);
  });

  it('navigates to the created promo code and clears saving', () => {
    facade.create(percentInput);

    expect(navigate).toHaveBeenCalledWith(['/loyalty/promos', 'promo-1']);
    expect(facade.saving()).toBe(false);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031) — the discount fields decide money.
  describe('command bodies on the wire', () => {
    it('serializes a percent code with the fraction the backend stores, not the UI percent', () => {
      facade.create(percentInput);

      const command: CreatePromoCodeCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreatePromoCodeCommand);
      expect(command.toJSON()).toEqual({
        code: 'SPRING20',
        type: PromoCodeType.PercentDiscount,
        discountPercent: 0.2,
        discountAmount: undefined,
        currencyId: undefined,
        minimumOrderAmount: 500,
        maxRedemptionsPerUser: 1,
        globalMaxRedemptions: 1000,
        validFrom: VALID_FROM.toISOString(),
        validUntil: VALID_UNTIL.toISOString(),
        description: 'Spring campaign',
      });
    });

    it('serializes a fixed-amount code with its currency and no percent', () => {
      facade.create({
        ...percentInput,
        type: PromoCodeType.FixedDiscount,
        discountPercentUi: undefined,
        discountAmount: 250,
        currencyId: 'cur-1',
      });

      const body = createMock.mock.calls[0][0].toJSON();
      expect(body.discountPercent).toBeUndefined();
      expect(body.discountAmount).toBe(250);
      expect(body.currencyId).toBe('cur-1');
    });

    it('serializes an update with the promo id and the active flag', () => {
      facade.update('promo-1', updateInput);

      const command: UpdatePromoCodeCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdatePromoCodeCommand);
      expect(command.toJSON()).toEqual({
        promoCodeId: 'promo-1',
        isActive: false,
        validFrom: VALID_FROM.toISOString(),
        validUntil: VALID_UNTIL.toISOString(),
        minimumOrderAmount: 500,
        maxRedemptionsPerUser: 2,
        globalMaxRedemptions: 1000,
        description: 'Paused',
      });
    });
  });
});
