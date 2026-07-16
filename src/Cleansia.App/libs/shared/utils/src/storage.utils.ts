/**
 * True when localStorage is actually usable. A bare `typeof` check is not
 * enough: Node 22+ exposes a global `localStorage` whose methods throw
 * unless the process was started with `--localstorage-file`, so SSR needs
 * the probing call inside a try/catch.
 */
export function isLocalStorageAvailable(): boolean {
  try {
    if (typeof localStorage === 'undefined') {
      return false;
    }
    localStorage.getItem('__probe__');
    return true;
  } catch {
    return false;
  }
}

export function getLocalStorageValueByKeyAsJSON(key: string) {
  if (!isLocalStorageAvailable()) {
    return null; // SSR safe
  }
  return localStorage.getItem(key);
}

export function getLocalStorageParsedValueByKey<T>(key: string): T | null {
  if (!isLocalStorageAvailable()) {
    return null; // SSR safe
  }
  const value = localStorage.getItem(key);
  if (!value) {
    return null;
  }
  return JSON.parse(value);
}

export function setLocalStorageValueByKey(key: string, value: unknown) {
  if (!isLocalStorageAvailable()) {
    return; // SSR safe
  }
  if (typeof value === 'string') {
    return localStorage.setItem(key, value);
  }
  localStorage.setItem(key, JSON.stringify(value));
}

export function removeLocalStorageValueByKey(key: string) {
  if (!isLocalStorageAvailable()) {
    return; // SSR safe
  }
  localStorage.removeItem(key);
}

export function clearLocalStorage() {
  if (!isLocalStorageAvailable()) {
    return; // SSR safe
  }
  localStorage.clear();
}
