import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FileDownloadService } from './file-download.service';

describe('FileDownloadService', () => {
  const blob = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
  let createObjectURL: jest.Mock;
  let revokeObjectURL: jest.Mock;
  let click: jest.SpyInstance;
  let createElement: jest.SpyInstance;

  beforeEach(() => {
    // jsdom has no object URLs; the service only needs the pair to hand back what it was given.
    createObjectURL = jest.fn().mockReturnValue('blob:incident');
    revokeObjectURL = jest.fn();
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true });
    click = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    createElement = jest.spyOn(document, 'createElement');
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  function service(platform: 'browser' | 'server'): FileDownloadService {
    TestBed.configureTestingModule({
      providers: [{ provide: PLATFORM_ID, useValue: platform }],
    });
    return TestBed.inject(FileDownloadService);
  }

  it('hands the blob to the browser as a named download and releases the object url', () => {
    service('browser').downloadBlob(blob, 'incident-user-1-20260914.pdf');

    expect(createObjectURL).toHaveBeenCalledWith(blob);
    const anchor = createElement.mock.results[0].value as HTMLAnchorElement;
    expect(anchor.download).toBe('incident-user-1-20260914.pdf');
    expect(anchor.href).toBe('blob:incident');
    expect(click).toHaveBeenCalledTimes(1);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:incident');
  });

  it('does nothing on the server', () => {
    service('server').downloadBlob(blob, 'incident-user-1-20260914.pdf');

    expect(createObjectURL).not.toHaveBeenCalled();
    expect(createElement).not.toHaveBeenCalled();
    expect(click).not.toHaveBeenCalled();
  });
});
