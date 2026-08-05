import { TestBed } from '@angular/core/testing';
import {
  BlobFileDto,
  PartnerClient,
  PhotoType,
  SaveOrderPhotosCommand,
} from '@cleansia/partner-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { of } from 'rxjs';
import { OrderPhotosFacade } from './order-photos.facade';
import { StagedPhoto, buildPhotosToSave } from './order-photos.helpers';

const ORDER_ID = 'ord-1';

function stagedPhoto(): StagedPhoto {
  const file = new BlobFileDto();
  file.fileName = 'before.jpg';
  file.base64Content = 'data:image/jpeg;base64,AAAA';
  file.contentType = 'image/jpeg';

  return {
    file,
    photoType: PhotoType.Before,
    notes: 'Kitchen, north wall',
    preview: 'data:image/jpeg;base64,AAAA',
  };
}

describe('OrderPhotosFacade', () => {
  let orderClient: {
    savePhotos: jest.Mock;
    getPhotos: jest.Mock;
    deletePhoto: jest.Mock;
  };
  let snackbar: { showSuccessTranslated: jest.Mock };
  let dialogService: { confirmTranslated: jest.Mock };

  const createFacade = (): OrderPhotosFacade => {
    TestBed.configureTestingModule({
      providers: [
        OrderPhotosFacade,
        { provide: PartnerClient, useValue: { orderClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    return TestBed.inject(OrderPhotosFacade);
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    orderClient = {
      savePhotos: jest.fn().mockReturnValue(of({})),
      getPhotos: jest.fn().mockReturnValue(of({})),
      deletePhoto: jest.fn().mockReturnValue(of({})),
    };
    snackbar = { showSuccessTranslated: jest.fn() };
    dialogService = { confirmTranslated: jest.fn().mockReturnValue(of(true)) };
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a save with the order id and every staged photo', () => {
      const facade = createFacade();

      facade.savePhotos(ORDER_ID, [stagedPhoto()], () => undefined);

      const command: SaveOrderPhotosCommand =
        orderClient.savePhotos.mock.calls[0][0];
      expect(command).toBeInstanceOf(SaveOrderPhotosCommand);
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        photos: [
          {
            photoType: PhotoType.Before,
            notes: 'Kitchen, north wall',
            file: {
              fileName: 'before.jpg',
              base64Content: 'data:image/jpeg;base64,AAAA',
              contentType: 'image/jpeg',
            },
          },
        ],
      });
    });

    it('carries the photo type, the notes and the file onto each entry', () => {
      const [photo] = buildPhotosToSave([stagedPhoto()]);

      expect(photo.toJSON()).toEqual({
        photoType: PhotoType.Before,
        notes: 'Kitchen, north wall',
        file: {
          fileName: 'before.jpg',
          base64Content: 'data:image/jpeg;base64,AAAA',
          contentType: 'image/jpeg',
        },
      });
    });
  });

  it('does not call the endpoint with nothing staged', () => {
    const facade = createFacade();

    facade.savePhotos(ORDER_ID, [], () => undefined);

    expect(orderClient.savePhotos).not.toHaveBeenCalled();
  });

  it('reports success and re-reads the gallery once the save lands', () => {
    const facade = createFacade();
    const onSuccess = jest.fn();

    facade.savePhotos(ORDER_ID, [stagedPhoto()], onSuccess);

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'global.messages.orders.photos_saved'
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(orderClient.getPhotos).toHaveBeenCalledWith(ORDER_ID);
    expect(facade.saving()).toBe(false);
  });
});
