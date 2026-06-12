import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  NotificationPreferencesDto,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { Subject, of, throwError } from 'rxjs';
import { NotificationPreferencesFacade } from './notification-preferences.facade';
import { NOTIFICATION_PREFERENCE_CATEGORIES } from './notification-preferences.models';

describe('NotificationPreferencesFacade', () => {
  let facade: NotificationPreferencesFacade;
  let preferencesClient: { getMine: jest.Mock; update: jest.Mock };
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const allOn = NotificationPreferencesDto.fromJS({
    orderUpdates: true,
    cleanerOnTheWay: true,
    orderCompleted: true,
    orderCancelled: true,
    refundIssued: true,
    membershipExpiring: true,
    membershipCancelled: true,
    tierUpgrade: true,
    promo: true,
    disputeReply: true,
    recurringScheduled: true,
  });

  beforeEach(() => {
    preferencesClient = { getMine: jest.fn(), update: jest.fn() };
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        NotificationPreferencesFacade,
        {
          provide: CustomerClient,
          useValue: { notificationPreferencesClient: preferencesClient },
        },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });

    facade = TestBed.inject(NotificationPreferencesFacade);
  });

  it('covers all 11 categories exactly once', () => {
    const fields = NOTIFICATION_PREFERENCE_CATEGORIES.map((c) => c.field);
    expect(fields).toHaveLength(11);
    expect(new Set(fields).size).toBe(11);
  });

  it('populates the 11 preference values from getMine', () => {
    preferencesClient.getMine.mockReturnValue(
      of(
        NotificationPreferencesDto.fromJS({
          ...allOn.toJSON(),
          promo: false,
          disputeReply: false,
        })
      )
    );

    facade.load();

    const prefs = facade.preferences();
    expect(prefs).not.toBeNull();
    expect(Object.keys(prefs ?? {})).toHaveLength(11);
    expect(prefs?.orderUpdates).toBe(true);
    expect(prefs?.promo).toBe(false);
    expect(prefs?.disputeReply).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('exposes the loading state while getMine is in flight', () => {
    const response$ = new Subject<NotificationPreferencesDto>();
    preferencesClient.getMine.mockReturnValue(response$.asObservable());

    facade.load();
    expect(facade.loading()).toBe(true);

    response$.next(allOn);
    response$.complete();
    expect(facade.loading()).toBe(false);
  });

  it('transitions to the error state and shows a snackbar when loading fails', () => {
    preferencesClient.getMine.mockReturnValue(throwError(() => new Error('x')));

    facade.load();

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.preferences()).toBeNull();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'pages.profile.notifications.load_error'
    );
  });

  it('flips a single category without touching the others', () => {
    preferencesClient.getMine.mockReturnValue(of(allOn));
    facade.load();

    facade.setPreference('promo', false);

    expect(facade.preferences()?.promo).toBe(false);
    expect(facade.preferences()?.orderUpdates).toBe(true);
  });

  it('saves all 11 fields via update and reflects the persisted response', () => {
    preferencesClient.getMine.mockReturnValue(of(allOn));
    facade.load();
    facade.setPreference('promo', false);

    const persisted = NotificationPreferencesDto.fromJS({
      ...allOn.toJSON(),
      promo: false,
    });
    preferencesClient.update.mockReturnValue(of(persisted));

    facade.save();

    expect(preferencesClient.update).toHaveBeenCalledTimes(1);
    const command = preferencesClient.update.mock.calls[0][0];
    for (const { field } of NOTIFICATION_PREFERENCE_CATEGORIES) {
      expect(typeof command[field]).toBe('boolean');
    }
    expect(command.promo).toBe(false);
    expect(command.orderUpdates).toBe(true);
    expect(facade.preferences()?.promo).toBe(false);
    expect(facade.saving()).toBe(false);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.profile.notifications.save_success'
    );
  });

  it('shows an error snackbar and keeps local values when saving fails', () => {
    preferencesClient.getMine.mockReturnValue(of(allOn));
    facade.load();
    facade.setPreference('tierUpgrade', false);
    preferencesClient.update.mockReturnValue(throwError(() => new Error('x')));

    facade.save();

    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'pages.profile.notifications.save_error'
    );
    expect(facade.saving()).toBe(false);
    expect(facade.preferences()?.tierUpgrade).toBe(false);
  });

  it('does not call update before the preferences are loaded', () => {
    facade.save();
    expect(preferencesClient.update).not.toHaveBeenCalled();
  });
});
