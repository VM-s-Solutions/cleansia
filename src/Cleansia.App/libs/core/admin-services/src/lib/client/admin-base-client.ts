import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  ADMINAPIBASEURL,
  AdminAuthClient,
  AdminCodeClient,
  AdminCompanyClient,
  AdminCountryClient,
  AdminCurrencyClient,
  AdminEmailTemplateClient,
  AdminEmployeeClient,
  AdminEmployeeDocumentClient,
  AdminInvoiceClient,
  AdminInvoiceTemplateClient,
  AdminLanguageClient,
  AdminOrderClient,
  AdminPackageClient,
  AdminPayPeriodClient,
  AdminReceiptTemplateClient,
  AdminReportClient,
  AdminServiceClient,
  AdminUserClient,
  IAdminAuthClient,
  IAdminCodeClient,
  IAdminCompanyClient,
  IAdminCountryClient,
  IAdminCurrencyClient,
  IAdminEmailTemplateClient,
  IAdminEmployeeClient,
  IAdminEmployeeDocumentClient,
  IAdminInvoiceClient,
  IAdminInvoiceTemplateClient,
  IAdminLanguageClient,
  IAdminOrderClient,
  IAdminPackageClient,
  IAdminPayPeriodClient,
  IAdminReceiptTemplateClient,
  IAdminReportClient,
  IAdminServiceClient,
  IAdminUserClient,
  ITypesClient,
  TypesClient,
} from './admin-client';

interface IAdminClient {
  adminAuthClient: IAdminAuthClient;
  adminCompanyClient: IAdminCompanyClient;
  adminEmployeeClient: IAdminEmployeeClient;
  adminCodeClient: IAdminCodeClient;
  adminCountryClient: IAdminCountryClient;
  adminCurrencyClient: IAdminCurrencyClient;
  adminEmailTemplateClient: IAdminEmailTemplateClient;
  adminEmployeeDocumentClient: IAdminEmployeeDocumentClient;
  adminInvoiceClient: IAdminInvoiceClient;
  adminInvoiceTemplateClient: IAdminInvoiceTemplateClient;
  adminLanguageClient: IAdminLanguageClient;
  adminOrderClient: IAdminOrderClient;
  adminPackageClient: IAdminPackageClient;
  adminPayPeriodClient: IAdminPayPeriodClient;
  adminReceiptTemplateClient: IAdminReceiptTemplateClient;
  adminReportClient: IAdminReportClient;
  adminServiceClient: IAdminServiceClient;
  adminUserClient: IAdminUserClient;
  emailTemplateTypesClient: ITypesClient;
}

@Injectable({
  providedIn: 'root',
})
export class AdminClient implements IAdminClient {
  private readonly httpClient: HttpClient = inject(HttpClient);
  private readonly apiBaseUrl: string =
    inject(ADMINAPIBASEURL, { optional: true }) ?? 'http://localhost:5001';

  adminAuthClient: IAdminAuthClient = new AdminAuthClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminCompanyClient: IAdminCompanyClient = new AdminCompanyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminEmployeeClient: IAdminEmployeeClient = new AdminEmployeeClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminCodeClient: IAdminCodeClient = new AdminCodeClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminEmployeeDocumentClient: IAdminEmployeeDocumentClient =
    new AdminEmployeeDocumentClient(this.httpClient, this.apiBaseUrl);
  adminInvoiceClient: IAdminInvoiceClient = new AdminInvoiceClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminCountryClient: IAdminCountryClient = new AdminCountryClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminLanguageClient: IAdminLanguageClient = new AdminLanguageClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminCurrencyClient: IAdminCurrencyClient = new AdminCurrencyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminOrderClient: IAdminOrderClient = new AdminOrderClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminPackageClient: IAdminPackageClient = new AdminPackageClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminPayPeriodClient: IAdminPayPeriodClient = new AdminPayPeriodClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminReportClient: IAdminReportClient = new AdminReportClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminServiceClient: IAdminServiceClient = new AdminServiceClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminUserClient: IAdminUserClient = new AdminUserClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminInvoiceTemplateClient: IAdminInvoiceTemplateClient =
    new AdminInvoiceTemplateClient(this.httpClient, this.apiBaseUrl);
  adminReceiptTemplateClient: IAdminReceiptTemplateClient =
    new AdminReceiptTemplateClient(this.httpClient, this.apiBaseUrl);
  adminEmailTemplateClient: IAdminEmailTemplateClient =
    new AdminEmailTemplateClient(this.httpClient, this.apiBaseUrl);
  emailTemplateTypesClient: ITypesClient = new TypesClient(
    this.httpClient,
    this.apiBaseUrl
  );
}
