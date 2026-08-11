import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminClient } from './admin-base-client';
import { ADMINAPIBASEURL } from './admin-client';

/**
 * The verbs are the contract, not an implementation detail: the masked read is
 * a GET so `payout.not_found` is silenced as an empty state, and the reveal is
 * a POST so the audit engine — which keys on commands — records it and the
 * rate-limit coverage guard covers it (ADR-0034 D8.4/D8.5). A facade spec that
 * mocks the client proves neither, so this one drives the real generated code.
 */
describe('admin payout-details routes', () => {
  let client: AdminClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Relative, as dev and the deployed apps are — one origin for the
        // HttpOnly SameSite=Strict auth cookie.
        { provide: ADMINAPIBASEURL, useValue: '' },
      ],
    });
    client = TestBed.inject(AdminClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the masked record with a GET', () => {
    client.adminEmployeeClient
      .payoutDetails('emp-1')
      .subscribe({ error: () => undefined });

    const request = httpMock.expectOne(
      '/api/AdminEmployee/emp-1/payout-details'
    );
    expect(request.request.method).toBe('GET');
    request.flush(new Blob(['{}'], { type: 'application/json' }));
  });

  it('reveals through a POST — the reveal is a command, never a second read', () => {
    client.payoutDetailsClient
      .reveal('emp-1')
      .subscribe({ error: () => undefined });

    const request = httpMock.expectOne(
      '/api/AdminEmployee/emp-1/payout-details/reveal'
    );
    expect(request.request.method).toBe('POST');
    request.flush(new Blob(['{}'], { type: 'application/json' }));
  });
});
