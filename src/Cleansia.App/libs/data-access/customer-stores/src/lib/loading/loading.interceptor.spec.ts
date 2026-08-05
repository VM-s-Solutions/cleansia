import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Store } from '@ngrx/store';
import {
  setCustomerLoadingOffAction,
  setCustomerLoadingOnAction,
} from './loading.actions';
import { CustomerLoadingInterceptorFn } from './loading.interceptor';

describe('customer LoadingInterceptorFn', () => {
  let dispatch: jest.Mock;
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    dispatch = jest.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([CustomerLoadingInterceptorFn])),
        provideHttpClientTesting(),
        { provide: Store, useValue: { dispatch } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('turns the indicator on when the request goes out and off when it completes', () => {
    http.get('/api/anything').subscribe();

    expect(dispatch).toHaveBeenCalledWith(setCustomerLoadingOnAction());
    expect(dispatch).not.toHaveBeenCalledWith(setCustomerLoadingOffAction());

    httpMock.expectOne('/api/anything').flush({});

    expect(dispatch).toHaveBeenCalledWith(setCustomerLoadingOffAction());
  });

  it('turns the indicator off when the request fails — otherwise it hangs on', () => {
    http.get('/api/anything').subscribe({ error: () => undefined });
    httpMock
      .expectOne('/api/anything')
      .flush('nope', { status: 500, statusText: 'Server Error' });

    expect(dispatch).toHaveBeenCalledWith(setCustomerLoadingOffAction());
  });
});
