import { TestBed } from '@angular/core/testing';
import { AdminClient, SendSitewidePromoCommand } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { Subject, of, throwError } from 'rxjs';
import { SendSitewidePromoResponse } from '@cleansia/admin-services';
import { SitewidePushFormFacade } from './sitewide-push-form.facade';
import {
  SitewidePushFormData,
  buildSendSitewidePromoCommand,
} from './sitewide-push-form.models';

describe('SitewidePushFormFacade', () => {
  let facade: SitewidePushFormFacade;
  let sendMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const formData: SitewidePushFormData = {
    titleEn: 'Hi',
    titleCs: 'Ahoj',
    titleSk: 'Ahoj',
    titleUk: 'Pryvit',
    titleRu: 'Privet',
    bodyEn: 'Body',
    bodyCs: 'Telo',
    bodySk: 'Telo',
    bodyUk: 'Tilo',
    bodyRu: 'Telo',
  };

  const command = buildSendSitewidePromoCommand(formData);

  beforeEach(() => {
    sendMock = jest.fn();
    confirmMock = jest.fn();
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        SitewidePushFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminMarketingClient: { sendSitewidePromo: sendMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
      ],
    });

    facade = TestBed.inject(SitewidePushFormFacade);
  });

  it('confirms then sends, shows success and runs onSuccess on the happy path', () => {
    confirmMock.mockReturnValue(of(true));
    sendMock.mockReturnValue(of({ enqueued: true }));
    const onSuccess = jest.fn();

    facade.submit(command, onSuccess);

    expect(confirmMock).toHaveBeenCalledWith(
      'pages.sitewide_push.confirm_send',
      'pages.sitewide_push.confirm_title'
    );
    expect(sendMock).toHaveBeenCalledWith(command);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.sitewide_push.send_success'
    );
    expect(onSuccess).toHaveBeenCalled();
    expect(facade.submitting()).toBe(false);
  });

  it('does not send when the confirm dialog is cancelled', () => {
    confirmMock.mockReturnValue(of(false));
    const onSuccess = jest.fn();

    facade.submit(command, onSuccess);

    expect(sendMock).not.toHaveBeenCalled();
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(onSuccess).not.toHaveBeenCalled();
    expect(facade.submitting()).toBe(false);
  });

  it('shows the error snackbar and resets submitting on send failure', () => {
    confirmMock.mockReturnValue(of(true));
    sendMock.mockReturnValue(throwError(() => new Error('boom')));
    const onSuccess = jest.fn();

    facade.submit(command, onSuccess);

    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'pages.sitewide_push.send_error'
    );
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(onSuccess).not.toHaveBeenCalled();
    expect(facade.submitting()).toBe(false);
  });

  it('flags submitting while the send is in flight and clears it on completion', () => {
    confirmMock.mockReturnValue(of(true));
    const inFlight = new Subject<SendSitewidePromoResponse>();
    sendMock.mockReturnValue(inFlight.asObservable());

    facade.submit(command, jest.fn());
    expect(facade.submitting()).toBe(true);

    inFlight.next(new SendSitewidePromoResponse({ enqueued: true }));
    inFlight.complete();
    expect(facade.submitting()).toBe(false);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // This pins the serialized body instead (ADR-0031) — and a locale silently missing from
  // the push is the whole defect this guards.
  it('serializes a title and a body for all five locales', () => {
    expect(command).toBeInstanceOf(SendSitewidePromoCommand);
    expect(command.toJSON()).toEqual({
      titleEn: 'Hi',
      titleCs: 'Ahoj',
      titleSk: 'Ahoj',
      titleUk: 'Pryvit',
      titleRu: 'Privet',
      bodyEn: 'Body',
      bodyCs: 'Telo',
      bodySk: 'Telo',
      bodyUk: 'Tilo',
      bodyRu: 'Telo',
    });
  });
});
