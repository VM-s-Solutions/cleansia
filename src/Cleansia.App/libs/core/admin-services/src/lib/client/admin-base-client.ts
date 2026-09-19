import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { errorToastSuppressingHttpClient } from '@cleansia/services';
import {
  AccessInstructionsClient,
  IAccessInstructionsClient,
  ADMINAPIBASEURL,
  AdminAuthClient,
  IAdminAuthClient,
  AdminCodeClient,
  IAdminCodeClient,
  AdminCompanyClient,
  IAdminCompanyClient,
  AdminCompanyLifecycleClient,
  IAdminCompanyLifecycleClient,
  AdminCountryClient,
  IAdminCountryClient,
  AdminCreditClient,
  IAdminCreditClient,
  AdminCurrencyClient,
  IAdminCurrencyClient,
  AdminEmailTemplateClient,
  IAdminEmailTemplateClient,
  AdminEmployeeClient,
  IAdminEmployeeClient,
  AdminEmployeeDocumentClient,
  IAdminEmployeeDocumentClient,
  AdminExtraClient,
  IAdminExtraClient,
  AdminInvoiceClient,
  IAdminInvoiceClient,
  AdminLanguageClient,
  IAdminLanguageClient,
  AdminLegalClient,
  IAdminLegalClient,
  AdminLoyaltyClient,
  IAdminLoyaltyClient,
  AdminLoyaltyTierClient,
  IAdminLoyaltyTierClient,
  AdminMarketingClient,
  IAdminMarketingClient,
  AdminNotificationClient,
  IAdminNotificationClient,
  AdminOrderClient,
  IAdminOrderClient,
  AdminPackageClient,
  IAdminPackageClient,
  AdminPayConfigClient,
  IAdminPayConfigClient,
  AdminPayPeriodClient,
  IAdminPayPeriodClient,
  AdminPayrollClient,
  IAdminPayrollClient,
  AdminPromoCodeClient,
  IAdminPromoCodeClient,
  AdminReferralClient,
  IAdminReferralClient,
  AdminReportClient,
  IAdminReportClient,
  AdminServiceClient,
  IAdminServiceClient,
  AdminTenantSettingsClient,
  IAdminTenantSettingsClient,
  AdminUserClient,
  IAdminUserClient,
  ApiClient,
  IApiClient,
  IPayoutDetailsClient,
  PayoutDetailsClient,
  ITypesClient,
  TypesClient,
} from './admin-client';

interface IAdminClient {
  adminAuthClient: IAdminAuthClient;
  adminCompanyClient: IAdminCompanyClient;
  adminCompanyLifecycleClient: IAdminCompanyLifecycleClient;
  adminEmployeeClient: IAdminEmployeeClient;
  adminCodeClient: IAdminCodeClient;
  adminCountryClient: IAdminCountryClient;
  adminCurrencyClient: IAdminCurrencyClient;
  adminEmailTemplateClient: IAdminEmailTemplateClient;
  adminEmployeeDocumentClient: IAdminEmployeeDocumentClient;
  adminExtraClient: IAdminExtraClient;
  adminInvoiceClient: IAdminInvoiceClient;
  adminLanguageClient: IAdminLanguageClient;
  adminLegalClient: IAdminLegalClient;
  adminOrderClient: IAdminOrderClient;
  accessInstructionsClient: IAccessInstructionsClient;
  adminPackageClient: IAdminPackageClient;
  adminPayPeriodClient: IAdminPayPeriodClient;
  adminPayrollClient: IAdminPayrollClient;
  adminReportClient: IAdminReportClient;
  adminServiceClient: IAdminServiceClient;
  adminTenantSettingsClient: IAdminTenantSettingsClient;
  adminUserClient: IAdminUserClient;
  emailTemplateTypesClient: ITypesClient;
  adminPayConfigClient: IAdminPayConfigClient;
  adminPromoCodeClient: IAdminPromoCodeClient;
  adminLoyaltyTierClient: IAdminLoyaltyTierClient;
  adminLoyaltyClient: IAdminLoyaltyClient;
  adminCreditClient: IAdminCreditClient;
  adminMarketingClient: IAdminMarketingClient;
  adminNotificationClient: IAdminNotificationClient;
  adminReferralClient: IAdminReferralClient;
  payoutDetailsClient: IPayoutDetailsClient;
  // The kitchen-sink generated client — hosts service-city CRUD + any
  // future endpoints that don't have their own dedicated controller.
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
  adminCompanyClient: IAdminCompanyClient = new AdminCompanyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminCompanyLifecycleClient: IAdminCompanyLifecycleClient = new AdminCompanyLifecycleClient(
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
  adminExtraClient: IAdminExtraClient = new AdminExtraClient(
    this.httpClient,
    this.apiBaseUrl
  );
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
  adminLegalClient: IAdminLegalClient = new AdminLegalClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminOrderClient: IAdminOrderClient = new AdminOrderClient(
    this.httpClient,
    this.apiBaseUrl
  );
  accessInstructionsClient: IAccessInstructionsClient = new AccessInstructionsClient(
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
  adminPayrollClient: IAdminPayrollClient = new AdminPayrollClient(
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
  adminTenantSettingsClient: IAdminTenantSettingsClient = new AdminTenantSettingsClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminUserClient: IAdminUserClient = new AdminUserClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminEmailTemplateClient: IAdminEmailTemplateClient =
    new AdminEmailTemplateClient(this.httpClient, this.apiBaseUrl);
  emailTemplateTypesClient: ITypesClient = new TypesClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminPayConfigClient: IAdminPayConfigClient = new AdminPayConfigClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminPromoCodeClient: IAdminPromoCodeClient = new AdminPromoCodeClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminLoyaltyTierClient: IAdminLoyaltyTierClient = new AdminLoyaltyTierClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminLoyaltyClient: IAdminLoyaltyClient = new AdminLoyaltyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminCreditClient: IAdminCreditClient = new AdminCreditClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminMarketingClient: IAdminMarketingClient = new AdminMarketingClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminReferralClient: IAdminReferralClient = new AdminReferralClient(
    this.httpClient,
    this.apiBaseUrl
  );
  adminNotificationClient: IAdminNotificationClient = new AdminNotificationClient(
    this.httpClient,
    this.apiBaseUrl
  );
  payoutDetailsClient: IPayoutDetailsClient = new PayoutDetailsClient(
    this.httpClient,
    this.apiBaseUrl
  );
  apiClient: IApiClient = new ApiClient(this.httpClient, this.apiBaseUrl);
}

/**
 * The same sub-clients over an `HttpClient` that opts every request out of the shared error toast.
 * Only the sub-clients with a call site that asked for silence are exposed here: the unread-count
 * poll behind the notification badge is made on the administrator's behalf every minute, and a toast
 * for a failed background read is noise about something nobody asked for.
 */
@Injectable({
  providedIn: 'root',
})
export class SilentFailureAdminClient {
  private readonly httpClient: HttpClient = errorToastSuppressingHttpClient();
  private readonly apiBaseUrl: string =
    inject(ADMINAPIBASEURL, { optional: true }) ?? 'http://localhost:5001';

  adminNotificationClient: IAdminNotificationClient = new AdminNotificationClient(
    this.httpClient,
    this.apiBaseUrl
  );
}
