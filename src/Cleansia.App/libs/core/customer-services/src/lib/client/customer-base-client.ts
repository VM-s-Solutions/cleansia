import { HttpClient } from '@angular/common/http';
import { inject, Injectable, InjectionToken } from '@angular/core';
import {
  CountryClient,
  DisputeClient,
  GdprClient,
  ICountryClient,
  IDisputeClient,
  IGdprClient,
  ILanguageClient,
  IPackageClient,
  IPaymentClient,
  IServiceClient,
  IUserClient,
  LanguageClient,
  PackageClient,
  PaymentClient,
  ServiceClient,
  UserClient,
} from '@cleansia/partner-services';
import {
  AuthClient as CustomerAuthClient,
  IAuthClient as ICustomerAuthClient,
  IOrderClient as ICustomerOrderClient,
  OrderClient as CustomerOrderClient,
} from './customer-client';

export const CUSTOMER_API_BASE_URL = new InjectionToken<string>(
  'CUSTOMER_API_BASE_URL'
);

interface ICustomerClient {
  authClient: ICustomerAuthClient;
  userClient: IUserClient;
  orderClient: ICustomerOrderClient;
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

  authClient: ICustomerAuthClient = new CustomerAuthClient(
    this.httpClient,
    this.apiBaseUrl
  );
  userClient: IUserClient = new UserClient(this.httpClient, this.apiBaseUrl);
  orderClient: ICustomerOrderClient = new CustomerOrderClient(
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
