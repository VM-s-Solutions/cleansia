export function getUniqueValuesFromArrays<T>(array: T[], key: keyof T): T[] {
  const seenIds = new Set<T[keyof T]>();
  const uniqueArray: T[] = [];

  for (const item of array) {
    const itemKey = item[key];
    if (!seenIds.has(itemKey)) {
      seenIds.add(itemKey);
      uniqueArray.push(item);
    }
  }

  return uniqueArray;
}

export function getObjectValues<T = unknown>(obj: Record<string, T>): T[] {
  return Object.values(obj);
}

export async function parseBlobToJson<T = unknown>(blob: Blob): Promise<T> {
  const text = await blob.text();
  return JSON.parse(text) as T;
}

export function convertEnumToArray<T extends Record<string, string | number>>(
  enumObj: T
): Array<T[keyof T]> {
  return Object.keys(enumObj)
    .filter((key) => isNaN(Number(key)))
    .map((key) => enumObj[key as keyof T]);
}
