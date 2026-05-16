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
  IServiceClient,
  IUserClient,
  LanguageClient,
  PackageClient,
  ServiceClient,
  UserClient,
} from '@cleansia/partner-services';
import {
  AuthClient as CustomerAuthClient,
  ConsentsClient,
  ExtraClient,
  IAuthClient as ICustomerAuthClient,
  IConsentsClient,
  IExtraClient,
  ILoyaltyClient,
  IMembershipClient,
  IOrderClient as ICustomerOrderClient,
  IPaymentClient,
  IPromoCodeClient,
  IRecurringBookingClient,
  IReferralClient,
  ISavedAddressClient,
  LoyaltyClient,
  MembershipClient,
  OrderClient as CustomerOrderClient,
  PaymentClient,
  PromoCodeClient,
  RecurringBookingClient,
  ReferralClient,
  SavedAddressClient,
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
  // NSwag groups by route segment, so the `/api/v1/Gdpr/consents/withdraw`
  // endpoint lives on `ConsentsClient` rather than `GdprClient`. Exposed
  // here so consumers can route Withdraw through the configured base URL.
  consentsClient: IConsentsClient;
  disputeClient: IDisputeClient;
  savedAddressClient: ISavedAddressClient;
  loyaltyClient: ILoyaltyClient;
  promoCodeClient: IPromoCodeClient;
  referralClient: IReferralClient;
  membershipClient: IMembershipClient;
  recurringBookingClient: IRecurringBookingClient;
  extraClient: IExtraClient;
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
  consentsClient: IConsentsClient = new ConsentsClient(
    this.httpClient,
    this.apiBaseUrl
  );
  disputeClient: IDisputeClient = new DisputeClient(
    this.httpClient,
    this.apiBaseUrl
  );
  savedAddressClient: ISavedAddressClient = new SavedAddressClient(
    this.httpClient,
    this.apiBaseUrl
  );
  loyaltyClient: ILoyaltyClient = new LoyaltyClient(
    this.httpClient,
    this.apiBaseUrl
  );
  promoCodeClient: IPromoCodeClient = new PromoCodeClient(
    this.httpClient,
    this.apiBaseUrl
  );
  referralClient: IReferralClient = new ReferralClient(
    this.httpClient,
    this.apiBaseUrl
  );
  // Plus subscription management. Previously consumers were injecting
  // `MembershipClient` directly, which uses NSwag's empty-string default
  // baseUrl — requests fell through to the SPA's own origin and returned
  // the index.html / 404 page. Always go through this wrapper so the
  // configured CUSTOMER_API_BASE_URL is honoured.
  membershipClient: IMembershipClient = new MembershipClient(
    this.httpClient,
    this.apiBaseUrl
  );
  recurringBookingClient: IRecurringBookingClient = new RecurringBookingClient(
    this.httpClient,
    this.apiBaseUrl
  );
  // Booking add-ons catalog (inside-oven, etc.) — exposed via the configured
  // base URL so it joins the same per-app routing as everything else above.
  extraClient: IExtraClient = new ExtraClient(this.httpClient, this.apiBaseUrl);
}
