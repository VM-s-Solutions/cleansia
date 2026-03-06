import { HttpClient } from '@angular/common/http';
import { inject, Injectable, InjectionToken } from '@angular/core';
import {
  AuthClient,
  CountryClient,
  DisputeClient,
  GdprClient,
  IAuthClient,
  ICountryClient,
  IDisputeClient,
  IGdprClient,
  ILanguageClient,
  IOrderClient,
  IPackageClient,
  IPaymentClient,
  IServiceClient,
  IUserClient,
  LanguageClient,
  OrderClient,
  PackageClient,
  PaymentClient,
  ServiceClient,
  UserClient,
} from '@cleansia/partner-services';

export const CUSTOMER_API_BASE_URL = new InjectionToken<string>(
  'CUSTOMER_API_BASE_URL'
);

interface ICustomerClient {
  authClient: IAuthClient;
  userClient: IUserClient;
  orderClient: IOrderClient;
  countryClient: ICountryClient;
  languageClient: ILanguageClient;
  packageClient: IPackageClient;
  paymentClient: IPaymentClient;
  serviceClient: IServiceClient;
  gdprClient: IGdprClient;
  disputeClient: IDisputeClient;
}

@Injectable({
  providedIn: 'root',
})
export class CustomerClient implements ICustomerClient {
  private readonly httpClient: HttpClient = inject(HttpClient);
  private readonly apiBaseUrl: string =
    inject(CUSTOMER_API_BASE_URL, { optional: true }) ??
    'http://localhost:5003';

  authClient: IAuthClient = new AuthClient(this.httpClient, this.apiBaseUrl);
  userClient: IUserClient = new UserClient(this.httpClient, this.apiBaseUrl);
  orderClient: IOrderClient = new OrderClient(
    this.httpClient,
    this.apiBaseUrl
  );
  countryClient: ICountryClient = new CountryClient(
    this.httpClient,
    this.apiBaseUrl
  );
  languageClient: ILanguageClient = new LanguageClient(
    this.httpClient,
    this.apiBaseUrl
  );
  packageClient: IPackageClient = new PackageClient(
    this.httpClient,
    this.apiBaseUrl
  );
  paymentClient: IPaymentClient = new PaymentClient(
    this.httpClient,
    this.apiBaseUrl
  );
  serviceClient: IServiceClient = new ServiceClient(
    this.httpClient,
    this.apiBaseUrl
  );
  gdprClient: IGdprClient = new GdprClient(this.httpClient, this.apiBaseUrl);
  disputeClient: IDisputeClient = new DisputeClient(
    this.httpClient,
    this.apiBaseUrl
  );
}
