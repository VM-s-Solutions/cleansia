export function getLocalStorageValueByKeyAsJSON(key: string) {
  if (typeof localStorage === 'undefined') {
    return null; // SSR safe
  }
  return localStorage.getItem(key);
}

export function getLocalStorageParsedValueByKey<T>(key: string): T | null {
  if (typeof localStorage === 'undefined') {
    return null; // SSR safe
  }
  const value = localStorage.getItem(key);
  if (!value) {
    return null;
  }
  return JSON.parse(value);
}

export function setLocalStorageValueByKey(key: string, value: unknown) {
  if (typeof localStorage === 'undefined') {
    return; // SSR safe
  }
  if (typeof value === 'string') {
    return localStorage.setItem(key, value);
  }
  localStorage.setItem(key, JSON.stringify(value));
}

export function removeLocalStorageValueByKey(key: string) {
  if (typeof localStorage === 'undefined') {
    return; // SSR safe
  }
  localStorage.removeItem(key);
}

export function clearLocalStorage() {
  if (typeof localStorage === 'undefined') {
    return; // SSR safe
  }
  localStorage.clear();
}
