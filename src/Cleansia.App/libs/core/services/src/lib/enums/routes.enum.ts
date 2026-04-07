export enum CleansiaPartnerRoute {
  HOME = '',
  DASHBOARD = 'dashboard',
  PROFILE = 'profile',
  ORDERS = 'orders',
  DISPUTES = 'disputes',
  INVOICES = 'invoices',
  LOGIN = 'login',
  REGISTER = 'register',
  FORGOT_PASSWORD = 'forgot-password',
  CONFIRM_EMAIL = 'confirm-email',
  GDPR = 'gdpr',
}

export enum CleansiaAdminRoute {
  HOME = '',
  LOGIN = 'login',
  UNAUTHORIZED = 'unauthorized',
  EMPLOYEE_MANAGEMENT = 'employee-management',
  PAY_PERIODS = 'pay-periods',
  ORDER_MANAGEMENT = 'order-management',
  INVOICE_MANAGEMENT = 'invoice-management',
  REPORTS = 'reports',
  SERVICE_MANAGEMENT = 'service-management',
  PACKAGE_MANAGEMENT = 'package-management',
  ADMIN_USER_MANAGEMENT = 'admin-user-management',
  LANGUAGE_MANAGEMENT = 'language-management',
  COUNTRY_MANAGEMENT = 'country-management',
  CURRENCY_MANAGEMENT = 'currency-management',
  COMPANY_INFO = 'company-info',
  TEMPLATE_MANAGEMENT = 'template-management',
  PAY_CONFIG_MANAGEMENT = 'pay-config-management',
}

export enum CleansiaCustomerRoute {
  HOME = '',
  LOGIN = 'login',
  REGISTER = 'register',
  CONFIRM_EMAIL = 'confirm-email',
  FORGOT_PASSWORD = 'forgot-password',
  SERVICES = 'services',
  ORDER = 'order',
  ORDERS = 'orders',
  PROFILE = 'profile',
  DISPUTES = 'disputes',
  GDPR = 'gdpr',
  CHECKOUT_SUCCESS = 'checkout/success',
  CHECKOUT_CANCEL = 'checkout/cancel',
  TRACK_ORDER = 'track-order',
  NOT_FOUND = 'not-found',
}

export enum CommonRoute {
  HOME = '',
  NOT_FOUND = 'not-found',
  LOGIN = 'login',
  LOGOUT = 'logout',
}
