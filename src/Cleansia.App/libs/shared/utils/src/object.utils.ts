export function getObjectValues<T = unknown>(obj: Record<string, T>): T[] {
  return Object.values(obj);
}

export async function parseBlobToJson<T = unknown>(blob: Blob): Promise<T> {
  const text = await blob.text();
  return JSON.parse(text) as T;
}
