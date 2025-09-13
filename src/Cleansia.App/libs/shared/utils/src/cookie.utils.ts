export function extractCookieValue(cookieName: string): string | null {
  if (typeof document === 'undefined') {
    return null; // SSR safe - return null when document is not available
  }

  const cookie = document.cookie
    .split(';')
    .find((cookie) => cookie.trim().startsWith(`${cookieName}=`));

  return cookie ? cookie.split('=')[1] : null;
}

export function setCookieValue(
  cookieName: string,
  cookieValue: string,
  expirationDate?: string
): void {
  if (typeof document === 'undefined') {
    return; // SSR safe - do nothing when document is not available
  }

  document.cookie = `${cookieName}=${cookieValue};expires=${
    expirationDate ? expirationDate : ''
  };path=/;Secure;SameSite=Strict`;
}

export function removeCookieValue(cookieName: string): void {
  if (typeof document === 'undefined') {
    return; // SSR safe - do nothing when document is not available
  }

  document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; Secure; SameSite=Strict`;
}
