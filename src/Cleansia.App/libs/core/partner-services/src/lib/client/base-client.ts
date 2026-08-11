import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { errorToastSuppressingHttpClient } from '@cleansia/services';
import {
  APIBASEURL,
  AuthClient,
  CodeClient,
  CountryClient,
  CurrencyClient,
  DashboardClient,
  EmployeeClient,
  EmployeePayrollClient,
  IAuthClient,
  ICodeClient,
  ICountryClient,
  ICurrencyClient,
  IDashboardClient,
  IEmployeeClient,
  IEmployeePayrollClient,
  ILanguageClient,
  IOrderClient,
  IPackageClient,
  IPayPeriodClient,
  IPaymentClient,
  IServiceClient,
  IUserClient,
  LanguageClient,
  OrderClient,
  PackageClient,
  PayPeriodClient,
  PaymentClient,
  ServiceClient,
  UserClient,
} from './partner-client';

const FALLBACK_API_BASE_URL = 'http://localhost:5000';

interface IPartnerClient {
  authClient: IAuthClient;
  codeClient: ICodeClient;
  userClient: IUserClient;
  orderClient: IOrderClient;
  countryClient: ICountryClient;
  currencyClient: ICurrencyClient;
  dashboardClient: IDashboardClient;
  employeeClient: IEmployeeClient;
  employeePayrollClient: IEmployeePayrollClient;
  languageClient: ILanguageClient;
  packageClient: IPackageClient;
  payPeriodClient: IPayPeriodClient;
  paymentClient: IPaymentClient;
  serviceClient: IServiceClient;
}

@Injectable({
  providedIn: 'root',
})
export class PartnerClient implements IPartnerClient {
  private readonly httpClient: HttpClient = inject(HttpClient);
  private readonly apiBaseUrl: string =
    inject(APIBASEURL, { optional: true }) ?? FALLBACK_API_BASE_URL;

  authClient: IAuthClient = new AuthClient(this.httpClient, this.apiBaseUrl);
  codeClient: ICodeClient = new CodeClient(this.httpClient, this.apiBaseUrl);
  userClient: IUserClient = new UserClient(this.httpClient, this.apiBaseUrl);
  orderClient: IOrderClient = new OrderClient(this.httpClient, this.apiBaseUrl);
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
  payPeriodClient: IPayPeriodClient = new PayPeriodClient(
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

/**
 * The same generated sub-clients over an `HttpClient` that stamps `SUPPRESS_ERROR_TOAST`, for calls
 * the caller promises the user will never see fail. Inject this instead of `PartnerClient` at that
 * call site; everything else keeps the shared snackbar. Add a sub-client here only together with
 * the call site that needs it.
 */
@Injectable({
  providedIn: 'root',
})
export class SilentFailurePartnerClient {
  private readonly httpClient: HttpClient = errorToastSuppressingHttpClient();
  private readonly apiBaseUrl: string =
    inject(APIBASEURL, { optional: true }) ?? FALLBACK_API_BASE_URL;

  userClient: IUserClient = new UserClient(this.httpClient, this.apiBaseUrl);
}
