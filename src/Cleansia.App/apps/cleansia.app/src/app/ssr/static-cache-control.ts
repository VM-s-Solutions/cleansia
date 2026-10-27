const HASHED_OUTPUT = /^(?:[^/]+-[A-Z0-9]{8}\.(?:js|css)|media\/[^/]+-[A-Z0-9]{8}\.[A-Za-z0-9]+)$/;

/**
 * The Cache-Control of a file the customer SSR serves from the browser build, by its path relative
 * to that build. A name carrying the build's content hash never changes, so it is kept for a year;
 * any other file keeps its address across builds and is revalidated against its ETag on every use.
 */
export function staticCacheControl(relativePath: string): string {
  return HASHED_OUTPUT.test(relativePath.replace(/\\/g, '/'))
    ? 'public, max-age=31536000, immutable'
    : 'no-cache';
}
