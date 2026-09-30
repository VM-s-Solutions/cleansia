export const environment = {
  apiHost: 'admin.cleansia.cz',
  apiPort: '443',
  apiProtocol: 'https',
  // Empty on purpose: /api is same-origin, proxied by the admin Static Web App to
  // its linked backend, which answers 401 to any request the SWA did not proxy.
  // → deploy/AZURE-DEV-RUNBOOK.md §11
  apiBaseUrl: '',
  blobStorageUrl: '',
  googleClientId: '',
  isDevelopment: false,
  sentryDsn: '',
  bugReportUrl: '',
};
