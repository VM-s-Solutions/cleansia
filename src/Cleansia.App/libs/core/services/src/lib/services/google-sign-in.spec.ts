import { getGoogleIdApi, GoogleIdApi } from './google-sign-in';

describe('getGoogleIdApi', () => {
  const windowWithGoogle = window as Window & { google?: unknown };

  afterEach(() => {
    delete windowWithGoogle.google;
  });

  it('is absent until the GSI script has loaded', () => {
    expect(getGoogleIdApi()).toBeUndefined();
  });

  it('is absent while another Google script owns the global but identity has not loaded', () => {
    windowWithGoogle.google = { maps: {} };
    expect(getGoogleIdApi()).toBeUndefined();
  });

  it('is the identity API once the script has attached it', () => {
    const id: GoogleIdApi = { initialize: jest.fn(), renderButton: jest.fn() };
    windowWithGoogle.google = { accounts: { id } };
    expect(getGoogleIdApi()).toBe(id);
  });
});
