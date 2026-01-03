import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  ADMINAPIBASEURL,
  AdminAuthClient,
  AdminCodeClient,
  AdminEmployeeClient,
  AdminEmployeeDocumentClient,
  AdminInvoiceClient,
  AdminOrderClient,
  AdminPayPeriodClient,
  AdminReportClient,
  AdminServiceClient,
  ApiClient,
  IAdminAuthClient,
  IAdminCodeClient,
  IAdminEmployeeClient,
  IAdminEmployeeDocumentClient,
  IAdminInvoiceClient,
  IAdminOrderClient,
  IAdminPayPeriodClient,
  IAdminReportClient,
  IAdminServiceClient,
  IApiClient,
} from './admin-client';

interface IAdminClient {
  adminAuthClient: IAdminAuthClient;
  adminEmployeeClient: IAdminEmployeeClient;
  adminCodeClient: IAdminCodeClient;
  adminEmployeeDocumentClient: IAdminEmployeeDocumentClient;
  adminInvoiceClient: IAdminInvoiceClient;
  adminOrderClient: IAdminOrderClient;
  adminPayPeriodClient: IAdminPayPeriodClient;
  adminReportClient: IAdminReportClient;
  adminServiceClient: IAdminServiceClient;
  apiClient: IApiClient;
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
  adminOrderClient: IAdminOrderClient = new AdminOrderClient(
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
  apiClient: IApiClient = new ApiClient(this.httpClient, this.apiBaseUrl);
}
