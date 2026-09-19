import { MarketListItem } from '@cleansia/partner-services';

const MARKET_LABEL_SEPARATOR = ' · ';

export function marketOptionLabel(market: MarketListItem, lang: string): string {
  const name = market.translations?.[lang]?.name || market.name || market.isoCode;
  return `${name}${MARKET_LABEL_SEPARATOR}${market.currencyCode ?? ''}`;
}

export function defaultMarket(markets: readonly MarketListItem[]): MarketListItem | null {
  return markets.find((market) => market.isDefault) ?? markets[0] ?? null;
}

export interface PasswordCheck {
  hasLetter: boolean;
  hasNumber: boolean;
  hasMinLength: boolean;
  arePasswordsEqual?: boolean;
}

export function checkIfPasswordsValid(
  password: string,
  confirmPassword?: string
): PasswordCheck {
  return {
    hasLetter: /[a-zA-Z]/.test(password),
    hasNumber: /\d/.test(password),
    hasMinLength: password.length >= 8,
    arePasswordsEqual: confirmPassword ? password === confirmPassword : false,
  };
}
