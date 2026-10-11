/**
 * jsdom 26, the copy Jest's environment (jest-environment-jsdom) loads, implements neither
 * `Blob.prototype.text` nor `Blob.prototype.arrayBuffer`, and the partner and admin NSwag clients,
 * plus the customer client's file endpoints, read error bodies with `responseType: 'blob'`.
 * Without this, a spec that asserts a translated refusal exercises `HttpErrorInterceptorFn`'s
 * `.catch` arm and reports `api.common.error_occurred` — which passes just as well when the key is
 * wrong, missing or renamed.
 *
 * Loaded for every project through `jest.preset.js`; `http-error.interceptor.spec.ts`'s blob-branch
 * cases go red if it stops being loaded.
 */
if (typeof Blob.prototype.text !== 'function') {
  Blob.prototype.text = function (this: Blob): Promise<string> {
    return new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(reader.error);
      reader.readAsText(this);
    });
  };
}
