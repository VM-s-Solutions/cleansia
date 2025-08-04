import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  API_BASE_URL,
  CodeClient,
  CurrencyClient,
  ICodeClient,
  ICurrencyClient,
  ILanguageClient,
  IOrderClient,
  IPackageClient,
  IPaymentClient,
  IServiceClient,
  LanguageClient,
  OrderClient,
  PackageClient,
  PaymentClient,
  ServiceClient,
} from './client';

interface IClient {
  codeClient: ICodeClient;
  currencyClient: ICurrencyClient;
  languageClient: ILanguageClient;
  orderClient: IOrderClient;
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
    inject(API_BASE_URL, { optional: true }) ?? 'http://localhost:5000';

  codeClient: ICodeClient = new CodeClient(this.httpClient, this.apiBaseUrl);
  currencyClient: ICurrencyClient = new CurrencyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  languageClient: ILanguageClient = new LanguageClient(
    this.httpClient,
    this.apiBaseUrl
  );
  orderClient: IOrderClient = new OrderClient(this.httpClient, this.apiBaseUrl);
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
