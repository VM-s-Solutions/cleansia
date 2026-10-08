import { HttpClient, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, Observable } from 'rxjs';
import {
  AddressSearchClient,
  ApiException,
  CurrencyClient,
  CurrencyListItem,
  GetMembershipPlansResponse,
  MarketClient,
  MarketListItem,
  MembershipClient,
  OrderClient,
  PackageClient,
  PackageListItem,
  ProblemDetails,
  ServiceClient,
  ServiceListItem,
  Translation,
} from './customer-client';

const BASE_URL = 'https://api.cleansia.test';

describe('customer generated transport', () => {
  let http: HttpClient;
  let requests: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
  });

  afterEach(() => requests.verify());

  const catalogues: {
    name: string;
    path: string;
    read: (http: HttpClient) => Observable<unknown[]>;
    body: Record<string, unknown>;
    model: new () => object;
  }[] = [
    {
      name: 'market',
      path: '/api/Market/GetOverview',
      read: (http) => new MarketClient(http, BASE_URL).getOverview(),
      body: { countryId: 'cz', isoCode: 'CZE', currencyCode: 'CZK', isDefault: true },
      model: MarketListItem,
    },
    {
      name: 'currency',
      path: '/api/Currency/GetOverview',
      read: (http) => new CurrencyClient(http, BASE_URL).getOverview(),
      body: { id: 'czk', code: 'CZK', isDefault: true },
      model: CurrencyListItem,
    },
    {
      name: 'service',
      path: '/api/Service/GetOverview?countryId=cz',
      read: (http) => new ServiceClient(http, BASE_URL).getOverview('cz'),
      body: { id: 'service', name: 'Standard' },
      model: ServiceListItem,
    },
    {
      name: 'package',
      path: '/api/Package/GetOverview?countryId=cz',
      read: (http) => new PackageClient(http, BASE_URL).getOverview('cz'),
      body: { id: 'package', name: 'Deep clean' },
      model: PackageListItem,
    },
    {
      name: 'plan',
      path: '/api/Membership/GetPlans?countryId=cz',
      read: (http) => new MembershipClient(http, BASE_URL).getPlans('cz'),
      body: { code: 'plus', price: 299, currencyCode: 'CZK' },
      model: GetMembershipPlansResponse,
    },
  ];

  it.each(catalogues)('keeps $name DTOs after a JSON transfer-body round trip', async ({ read, path, body, model }) => {
    const result = firstValueFrom(read(http));
    const request = requests.expectOne(BASE_URL + path);
    const responseBody = request.request.responseType === 'blob'
      ? new Blob([JSON.stringify([body])], { type: 'application/json' })
      : JSON.stringify([body]);
    const transferred: { b: string | Record<string, never> } = JSON.parse(
      JSON.stringify({ b: responseBody })
    );

    request.flush(request.request.responseType === 'blob'
      ? new Blob([String(transferred.b)], { type: 'application/json' })
      : transferred.b);

    const items = await result;
    expect(request.request.responseType).toBe('text');
    expect(items).toHaveLength(1);
    expect(items[0]).toBeInstanceOf(model);
    expect(items[0]).toMatchObject(body);
  });

  it('preserves nested typed market translations', async () => {
    const result = firstValueFrom(new MarketClient(http, BASE_URL).getOverview());
    const request = requests.expectOne(BASE_URL + '/api/Market/GetOverview');
    const body = JSON.stringify([{ isoCode: 'CZE', translations: { cs: { name: 'Česko' } } }]);
    request.flush(request.request.responseType === 'blob' ? new Blob([body]) : body);

    const markets = await result;
    expect(markets[0].translations?.['cs']).toBeInstanceOf(Translation);
    expect(markets[0].translations?.['cs'].name).toBe('Česko');
  });

  it('preserves the existing null result for a 204 catalogue response', async () => {
    const result = firstValueFrom(new MarketClient(http, BASE_URL).getOverview());
    requests.expectOne(BASE_URL + '/api/Market/GetOverview')
      .flush(null, { status: 204, statusText: 'No Content' });
    await expect(result).resolves.toBeNull();
  });

  it('rejects malformed JSON rather than emitting an empty catalogue', async () => {
    const result = firstValueFrom(new MarketClient(http, BASE_URL).getOverview());
    const request = requests.expectOne(BASE_URL + '/api/Market/GetOverview');
    request.flush(request.request.responseType === 'blob' ? new Blob(['not JSON']) : 'not JSON');
    await expect(result).rejects.toBeInstanceOf(SyntaxError);
  });

  it('preserves typed ProblemDetails and backend error codes', async () => {
    const result = firstValueFrom(new MarketClient(http, BASE_URL).getOverview());
    const request = requests.expectOne(BASE_URL + '/api/Market/GetOverview');
    const body = { status: 400, detail: 'Invalid market', errors: { market: 'market.not_found' } };
    request.flush(request.request.responseType === 'blob'
      ? new Blob([JSON.stringify(body)]) : JSON.stringify(body),
    { status: 400, statusText: 'Bad Request' });

    await expect(result).rejects.toBeInstanceOf(ProblemDetails);
    await expect(result).rejects.toMatchObject(body);
  });

  it('preserves unexpected-error status, raw response and headers', async () => {
    const result = firstValueFrom(new MarketClient(http, BASE_URL).getOverview());
    const request = requests.expectOne(BASE_URL + '/api/Market/GetOverview');
    request.flush(request.request.responseType === 'blob'
      ? new Blob(['gateway refused']) : 'gateway refused', {
      status: 502,
      statusText: 'Bad Gateway',
      headers: { 'x-request-id': 'receipt' },
    });

    await expect(result).rejects.toBeInstanceOf(ApiException);
    await expect(result).rejects.toMatchObject({
      status: 502,
      response: 'gateway refused',
      headers: { 'x-request-id': 'receipt' },
    });
  });

  it.each(['map', 'receipt'])('preserves the binary %s response', async (kind) => {
    const result = firstValueFrom(kind === 'map'
      ? new AddressSearchClient(http, BASE_URL).map(50, 14)
      : new OrderClient(http, BASE_URL).downloadReceipt('order'));
    const request = requests.expectOne(BASE_URL + (kind === 'map'
      ? '/api/AddressSearch/map?lat=50&lng=14'
      : '/api/Order/DownloadReceipt?OrderId=order'));
    const bytes = new Blob([new Uint8Array([0, 255, 128, 65])]);
    request.flush(bytes, {
      status: 206,
      statusText: 'Partial Content',
      headers: { 'content-disposition': 'attachment; filename="receipt.pdf"' },
    });

    const file = await result;
    expect(request.request.responseType).toBe('blob');
    expect(file.data).toBe(bytes);
    expect(file.data.size).toBe(4);
    expect(file.status).toBe(206);
    expect(file.fileName).toBe('receipt.pdf');
    expect(file.headers?.['content-disposition']).toContain('receipt.pdf');
  });

  it('preserves typed JSON errors from a binary endpoint', async () => {
    const result = firstValueFrom(new OrderClient(http, BASE_URL).downloadReceipt('order'));
    const body = { status: 400, errors: { order: 'order.not_found' } };
    requests.expectOne(BASE_URL + '/api/Order/DownloadReceipt?OrderId=order').flush(
      new Blob([JSON.stringify(body)], { type: 'application/json' }),
      { status: 400, statusText: 'Bad Request' }
    );

    await expect(result).rejects.toBeInstanceOf(ProblemDetails);
    await expect(result).rejects.toMatchObject(body);
  });
});
