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

export function setLocalStorageValueByKey(key: string, value: unknown) {
  if (!isLocalStorageAvailable()) {
    return; // SSR safe
  }
  if (typeof value === 'string') {
    return localStorage.setItem(key, value);
  }
  localStorage.setItem(key, JSON.stringify(value));
}
