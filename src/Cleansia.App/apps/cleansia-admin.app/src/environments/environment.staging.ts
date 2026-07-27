// apiBaseUrl here is also the target of proxy.devremote.conf.json — keep the
// two in sync when the deployed dev host changes.
export const environment = {
  apiHost: 'api-cleansia-admin-weu-dev.azurewebsites.net',
  apiPort: '443',
  apiProtocol: 'https',
  apiBaseUrl: 'https://admin-api.dev.cleansia.cz',
  blobStorageUrl: '',
  googleClientId: '',
  betaGateEnabled: false,
  betaGateUrl: '',
  isDevelopment: false,
  sentryDsn: '',
  bugReportUrl:
    'https://docs.google.com/spreadsheets/d/1k4IbmrKPkZo79D4pDzukjUjQqY-ipnXqkUSSFoHtfFg/edit?usp=sharing',
};
