import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  LoyaltyTier,
  PreviewTierThresholdImpactQuery,
  UpdateTierConfigCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { TierConfigsFacade, TierConfigUpdateInput } from './tier-configs.facade';

describe('TierConfigsFacade', () => {
  let facade: TierConfigsFacade;
  let getAllMock: jest.Mock;
  let updateMock: jest.Mock;
  let previewMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const input: TierConfigUpdateInput = {
    lifetimePointsThreshold: 500,
    discountPercentUi: 12.5,
    minimumOrderAmountForDiscount: 300,
    perksJson: '{"freeDelivery":true}',
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getAllMock = jest.fn().mockReturnValue(of({ tiers: [] }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'tier-1' }));
    previewMock = jest.fn().mockReturnValue(of({ impacts: [] }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        TierConfigsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLoyaltyTierClient: {
              getAll: getAllMock,
              update: updateMock,
              previewThresholdImpact: previewMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(TierConfigsFacade);
  });

  it('starts empty and initially loading, with no preview', () => {
    expect(facade.tiers()).toEqual([]);
    expect(facade.initialLoading()).toBe(true);
    expect(facade.previewResult()).toBeNull();
  });

  it('drops the initial-loading latch on an empty tier list', () => {
    facade.loadTiers();

    expect(facade.tiers()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('drops the initial-loading latch when the read fails', () => {
    getAllMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadTiers();

    expect(facade.tiers()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('re-reads the tiers and runs the callback once a save lands', () => {
    const onSuccess = jest.fn();

    facade.update('tier-1', input, onSuccess);

    expect(getAllMock).toHaveBeenCalledTimes(1);
    expect(onSuccess).toHaveBeenCalled();
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.loyalty_tiers.form.success'
    );
    expect(facade.saving()).toBe(false);
  });

  it('reports the save error and does not re-read when a save fails', () => {
    updateMock.mockReturnValue(throwError(() => new Error('boom')));
    const onSuccess = jest.fn();

    facade.update('tier-1', input, onSuccess);

    expect(getAllMock).not.toHaveBeenCalled();
    expect(onSuccess).not.toHaveBeenCalled();
    expect(snackbar.showError).toHaveBeenCalledWith(
      'pages.loyalty_tiers.form.error.generic'
    );
    expect(facade.saving()).toBe(false);
  });

  it('holds the preview impacts and clears them on demand', () => {
    previewMock.mockReturnValue(of({ impacts: [{ tier: LoyaltyTier.GoldPolisher }] }));

    facade.previewThresholds(0, 100, 200, 300);
    expect(facade.previewResult()).toEqual([{ tier: LoyaltyTier.GoldPolisher }]);

    facade.clearPreviewResult();
    expect(facade.previewResult()).toBeNull();
  });

  it('reports the preview error and leaves the result null', () => {
    previewMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.previewThresholds(0, 100, 200, 300);

    expect(facade.previewResult()).toBeNull();
    expect(facade.previewing()).toBe(false);
    expect(snackbar.showError).toHaveBeenCalledWith(
      'pages.loyalty_tiers.preview.error.preview'
    );
  });

  it('issues no update at all when no threshold actually moved', () => {
    getAllMock.mockReturnValue(
      of({ tiers: [{ id: 't-1', tier: LoyaltyTier.BronzeCleaner, lifetimePointsThreshold: 0 }] })
    );
    facade.loadTiers();
    const onAllSuccess = jest.fn();

    facade.applyThresholdChanges(
      { [LoyaltyTier.BronzeCleaner]: 0 } as Record<LoyaltyTier, number>,
      onAllSuccess
    );

    expect(updateMock).not.toHaveBeenCalled();
    expect(onAllSuccess).toHaveBeenCalled();
  });

  describe('command bodies on the wire', () => {
    it('serializes a tier save with the UI percent converted to a fraction', () => {
      facade.update('tier-1', input);

      const command: UpdateTierConfigCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateTierConfigCommand);
      expect(command.toJSON()).toEqual({
        tierConfigId: 'tier-1',
        lifetimePointsThreshold: 500,
        discountPercent: 0.125,
        minimumOrderAmountForDiscount: 300,
        perksJson: '{"freeDelivery":true}',
      });
    });

    it('leaves the optional minimum and perks undefined when the form omits them', () => {
      facade.update('tier-1', {
        lifetimePointsThreshold: 100,
        discountPercentUi: 0,
      });

      const command: UpdateTierConfigCommand = updateMock.mock.calls[0][1];
      expect(command.toJSON()).toEqual({
        tierConfigId: 'tier-1',
        lifetimePointsThreshold: 100,
        discountPercent: 0,
        minimumOrderAmountForDiscount: undefined,
        perksJson: undefined,
      });
    });

    it('serializes the threshold preview with all four tier thresholds', () => {
      facade.previewThresholds(0, 100, 500, 2000);

      const query: PreviewTierThresholdImpactQuery = previewMock.mock.calls[0][0];
      expect(query).toBeInstanceOf(PreviewTierThresholdImpactQuery);
      expect(query.toJSON()).toEqual({
        bronzeThreshold: 0,
        silverThreshold: 100,
        goldThreshold: 500,
        platinumThreshold: 2000,
      });
    });

    it('carries the untouched discount, minimum and perks through a bulk threshold apply', () => {
      getAllMock.mockReturnValue(
        of({
          tiers: [
            {
              id: 't-1',
              tier: LoyaltyTier.SilverMopper,
              lifetimePointsThreshold: 100,
              discountPercent: 0.05,
              minimumOrderAmountForDiscount: 250,
              perksJson: '{"priority":true}',
            },
          ],
        })
      );
      facade.loadTiers();

      facade.applyThresholdChanges({
        [LoyaltyTier.SilverMopper]: 150,
      } as Record<LoyaltyTier, number>);

      const command: UpdateTierConfigCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateTierConfigCommand);
      expect(command.toJSON()).toEqual({
        tierConfigId: 't-1',
        lifetimePointsThreshold: 150,
        discountPercent: 0.05,
        minimumOrderAmountForDiscount: 250,
        perksJson: '{"priority":true}',
      });
    });
  });
});
