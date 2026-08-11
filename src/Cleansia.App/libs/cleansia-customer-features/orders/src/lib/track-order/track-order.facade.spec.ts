import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
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
  it('sends every lookup pair in the batch body', () => {
    facade
      .lookupBatch([
        { orderId: 'ord-1', email: 'jan@example.com' },
        { orderId: 'ord-2', email: 'eva@example.com' },
      ])
      .subscribe();

    const request = httpMock.expectOne(`${BASE_URL}/api/Order/LookupBatch`);
    expect(JSON.parse(request.request.body)).toEqual({
      items: [
        { orderId: 'ord-1', email: 'jan@example.com' },
        { orderId: 'ord-2', email: 'eva@example.com' },
      ],
    });

    request.flush(new Blob([JSON.stringify({ orders: [] })]));
  });

  it('sends an empty items array rather than omitting the member', () => {
    facade.lookupBatch([]).subscribe();

    const request = httpMock.expectOne(`${BASE_URL}/api/Order/LookupBatch`);
    expect(JSON.parse(request.request.body)).toEqual({ items: [] });

    request.flush(new Blob([JSON.stringify({ orders: [] })]));
  });
});
