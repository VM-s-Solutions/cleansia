/* eslint-disable @typescript-eslint/no-explicit-any */

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

export function getObjectValues(obj: any): any[] {
  return Object.values(obj);
}

export async function parseBlobToJson(blob: Blob): Promise<any> {
  const text = await blob.text();
  return JSON.parse(text);
}

export function convertEnumToArray(enumObj: any): any[] {
  return Object.keys(enumObj)
    .filter((key) => isNaN(Number(key)))
    .map((key) => enumObj[key]);
}
