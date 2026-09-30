import { TestBed } from '@angular/core/testing';
import { readFileSync } from 'fs';
import { join } from 'path';
import {
  GetOrderPhotosResponse,
  PartnerClient,
  PhotoType,
  ReportOrderLockoutCommand,
  SaveOrderPhotosCommand,
} from '@cleansia/partner-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, Subject, throwError } from 'rxjs';
import { OrderLockoutFacade } from './order-lockout.facade';
import { OrderPhotosFacade } from './order-photos.facade';

const ORDER_ID = 'ord-1';

describe('OrderLockoutFacade', () => {
  let orderClient: {
    reportLockout: jest.Mock;
    savePhotos: jest.Mock;
    getPhotos: jest.Mock;
    deletePhoto: jest.Mock;
  };
  let snackbar: { showSuccessTranslated: jest.Mock };

  const createFacade = (): OrderLockoutFacade => {
    TestBed.configureTestingModule({
      providers: [
        OrderLockoutFacade,
        OrderPhotosFacade,
        { provide: PartnerClient, useValue: { orderClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: jest.fn() } },
        { provide: TranslateService, useValue: { currentLang: 'cs', onLangChange: EMPTY } },
      ],
    });
    return TestBed.inject(OrderLockoutFacade);
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    orderClient = {
      reportLockout: jest.fn().mockReturnValue(of({ orderId: ORDER_ID })),
      savePhotos: jest.fn().mockReturnValue(of({})),
      getPhotos: jest.fn().mockReturnValue(of({})),
      deletePhoto: jest.fn(),
    };
    snackbar = { showSuccessTranslated: jest.fn() };
  });

  afterEach(() => jest.useRealTimers());

  describe('report', () => {
    it('sends the order and the trimmed note of the calls, confirms and re-reads the job', () => {
      const facade = createFacade();
      const onSettled = jest.fn();

      facade.report(ORDER_ID, '  Called 10:02 and 10:09, no answer  ', onSettled);

      const command: ReportOrderLockoutCommand = orderClient.reportLockout.mock.calls[0][0];
      expect(command).toBeInstanceOf(ReportOrderLockoutCommand);
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, callAttempts: 'Called 10:02 and 10:09, no answer' });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('global.messages.orders.lockout_reported');
      expect(onSettled).toHaveBeenCalledTimes(1);
      expect(facade.reporting()).toBe(false);
    });

    it('sends nothing for a blank note', () => {
      const facade = createFacade();

      facade.report(ORDER_ID, '   ', jest.fn());

      expect(orderClient.reportLockout).not.toHaveBeenCalled();
    });

    it('sends one report while the first is still in flight', () => {
      orderClient.reportLockout.mockReturnValue(new Subject());
      const facade = createFacade();

      facade.report(ORDER_ID, 'Called twice', jest.fn());
      facade.report(ORDER_ID, 'Called twice', jest.fn());

      expect(orderClient.reportLockout).toHaveBeenCalledTimes(1);
      expect(facade.reporting()).toBe(true);
    });

    // The interceptor has shown the refusal; a crew-mate may already have reported, so the job is re-read.
    it('re-reads the job after a refusal and confirms nothing', () => {
      orderClient.reportLockout.mockReturnValue(
        throwError(() => ({ errors: { OrderId: 'order.lockout.already_reported' } }))
      );
      const facade = createFacade();
      const onSettled = jest.fn();

      facade.report(ORDER_ID, 'Called twice', onSettled);

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(onSettled).toHaveBeenCalledTimes(1);
      expect(facade.reporting()).toBe(false);
    });

    it('does not re-read the job when the server never answered', () => {
      orderClient.reportLockout.mockReturnValue(throwError(() => new Error('offline')));
      const facade = createFacade();
      const onSettled = jest.fn();

      facade.report(ORDER_ID, 'Called twice', onSettled);

      expect(onSettled).not.toHaveBeenCalled();
      expect(facade.reporting()).toBe(false);
    });
  });

  describe('the entrance photo', () => {
    it('uploads the picked file as an entrance photo and re-reads the photos', () => {
      const facade = createFacade();
      const file = new File(['x'], 'door.jpg', { type: 'image/jpeg' });

      facade.uploadEntrancePhoto(ORDER_ID, 'data:image/jpeg;base64,AAAA', file);

      const command: SaveOrderPhotosCommand = orderClient.savePhotos.mock.calls[0][0];
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        photos: [
          {
            photoType: PhotoType.Entrance,
            notes: undefined,
            file: { fileName: 'door.jpg', base64Content: 'data:image/jpeg;base64,AAAA', contentType: 'image/jpeg' },
          },
        ],
      });
      expect(orderClient.getPhotos).toHaveBeenCalledWith(ORDER_ID);
    });

    it('lists only the entrance photos of the job', () => {
      orderClient.getPhotos.mockReturnValue(
        of(
          GetOrderPhotosResponse.fromJS({
            photos: [
              { id: 'p-before', photoType: PhotoType.Before },
              { id: 'p-door', photoType: PhotoType.Entrance },
              { id: 'p-after', photoType: PhotoType.After },
            ],
          })
        )
      );
      const facade = createFacade();

      facade.loadEntrancePhotos(ORDER_ID);

      expect(facade.entrancePhotos().map((photo) => photo.id)).toEqual(['p-door']);
    });
  });

  describe('the clock', () => {
    const OPENS_AT = new Date('2026-09-29T10:15:00Z');
    const at = (iso: string) => new Date(iso).getTime();

    it('moves to the moment the report opens', () => {
      jest.useFakeTimers();
      jest.setSystemTime(new Date('2026-09-29T10:10:00Z'));
      const facade = createFacade();

      facade.wakeAt(OPENS_AT);
      jest.advanceTimersByTime(5 * 60_000 - 1);
      expect(facade.now()).toBeLessThan(OPENS_AT.getTime());

      jest.advanceTimersByTime(1);
      expect(facade.now()).toBe(OPENS_AT.getTime());
    });

    // A sleeping phone stops the browser's timers but not the wall clock.
    it('re-reads the wall clock when the page is shown again after the phone slept through the opening', () => {
      jest.useFakeTimers();
      jest.setSystemTime(new Date('2026-09-29T10:02:00Z'));
      const facade = createFacade();
      facade.wakeAt(OPENS_AT);

      jest.setSystemTime(new Date('2026-09-29T10:20:00Z'));
      document.dispatchEvent(new Event('visibilitychange'));

      expect(facade.now()).toBe(at('2026-09-29T10:20:00Z'));
    });

    it('keeps waiting when a step lands before the opening, and still reaches it', () => {
      jest.useFakeTimers();
      jest.setSystemTime(new Date('2026-09-29T10:10:00Z'));
      const facade = createFacade();
      facade.wakeAt(OPENS_AT);

      jest.setSystemTime(new Date('2026-09-29T10:09:00Z'));
      jest.advanceTimersByTime(5 * 60_000);
      expect(facade.now()).toBeLessThan(OPENS_AT.getTime());

      jest.advanceTimersByTime(60_000);
      expect(facade.now()).toBe(OPENS_AT.getTime());
    });
  });
});

// Owner ruling 2026-09-28: the web asks for the camera rather than the gallery.
describe('the entrance photo picker', () => {
  it('asks the browser for the rear camera', () => {
    const template = readFileSync(join(__dirname, 'order-lockout.component.html'), 'utf8');
    const fileInputs = template.match(/<input[^>]*type="file"[^>]*>/g) ?? [];

    expect(fileInputs.length).toBe(1);
    expect(fileInputs[0]).toContain('capture="environment"');
  });
});
