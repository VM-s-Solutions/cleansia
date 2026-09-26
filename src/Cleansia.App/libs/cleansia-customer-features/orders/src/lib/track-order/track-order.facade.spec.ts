import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
import { GuestOrderService } from './guest-order.service';
import { TrackOrderFacade } from './track-order.facade';

const BASE_URL = 'https://api.test';

describe('TrackOrderFacade', () => {
  let facade: TrackOrderFacade;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        TrackOrderFacade,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CUSTOMER_API_BASE_URL, useValue: BASE_URL },
        { provide: GuestOrderService, useValue: { getAll: () => [], save: jest.fn() } },
      ],
    });

    facade = TestBed.inject(TrackOrderFacade);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // Every member of a generated query is optional, so a dropped assignment type-checks.
  // This reads the body off the wire instead (ADR-0031).
  it('sends the access token in the body without URL parameters', () => {
    facade.lookup('tok-123').subscribe();
    const request = httpMock.expectOne(`${BASE_URL}/api/Order/Lookup`);
    expect(request.request.method).toBe('POST');
    expect(request.request.params.keys()).toEqual([]);
    expect(JSON.parse(request.request.body)).toEqual({ accessToken: 'tok-123' });
    request.flush(new Blob([JSON.stringify({})]));
  });

  it('sends every remembered token in the batch body', () => {
    facade.lookupBatch(['tok-1', 'tok-2']).subscribe();

    const request = httpMock.expectOne(`${BASE_URL}/api/Order/LookupBatch`);
    expect(JSON.parse(request.request.body)).toEqual({
      accessTokens: ['tok-1', 'tok-2'],
    });

    request.flush(new Blob([JSON.stringify({ orders: [] })]));
  });

  it('sends an empty accessTokens array rather than omitting the member', () => {
    facade.lookupBatch([]).subscribe();

    const request = httpMock.expectOne(`${BASE_URL}/api/Order/LookupBatch`);
    expect(JSON.parse(request.request.body)).toEqual({ accessTokens: [] });

    request.flush(new Blob([JSON.stringify({ orders: [] })]));
  });
});
