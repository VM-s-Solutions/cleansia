import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  APIBASEURL,
  AuthClient,
  CodeClient,
  CountryClient,
  CurrencyClient,
  DashboardClient,
  DisputeClient,
  EmployeeClient,
  EmployeePayrollClient,
  IAuthClient,
  ICodeClient,
  ICountryClient,
  ICurrencyClient,
  IDashboardClient,
  IDisputeClient,
  IEmployeeClient,
  IEmployeePayrollClient,
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
} from './client';

interface IClient {
  authClient: IAuthClient;
  codeClient: ICodeClient;
  userClient: IUserClient;
  orderClient: IOrderClient;
  disputeClient: IDisputeClient;
  countryClient: ICountryClient;
  currencyClient: ICurrencyClient;
  dashboardClient: IDashboardClient;
  employeeClient: IEmployeeClient;
  employeePayrollClient: IEmployeePayrollClient;
  languageClient: ILanguageClient;
  packageClient: IPackageClient;
  paymentClient: IPaymentClient;
  serviceClient: IServiceClient;
}

@Injectable({
  providedIn: 'root',
})
export class Client implements IClient {
  private readonly httpClient: HttpClient = inject(HttpClient);
  private readonly apiBaseUrl: string =
    inject(APIBASEURL, { optional: true }) ?? 'http://localhost:5000';

  authClient: IAuthClient = new AuthClient(this.httpClient, this.apiBaseUrl);
  codeClient: ICodeClient = new CodeClient(this.httpClient, this.apiBaseUrl);
  userClient: IUserClient = new UserClient(this.httpClient, this.apiBaseUrl);
  orderClient: IOrderClient = new OrderClient(this.httpClient, this.apiBaseUrl);
  disputeClient: IDisputeClient = new DisputeClient(this.httpClient, this.apiBaseUrl);
  countryClient: ICountryClient = new CountryClient(
    this.httpClient,
    this.apiBaseUrl
  );
  currencyClient: ICurrencyClient = new CurrencyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  dashboardClient: IDashboardClient = new DashboardClient(
    this.httpClient,
    this.apiBaseUrl
  );
  employeeClient: IEmployeeClient = new EmployeeClient(
    this.httpClient,
    this.apiBaseUrl
  );
  employeePayrollClient: IEmployeePayrollClient = new EmployeePayrollClient(
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
}
