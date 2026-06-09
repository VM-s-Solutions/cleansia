export interface PackageServiceWeightRow {
  id: string;
  name: string;
  weight: number;
}

export interface DerivedServiceGross {
  id: string;
  name: string;
  weight: number;
  gross: number;
}

export const PACKAGE_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'package.invalid_weight': 'errors.package.invalid_weight',
  'package.not_found': 'errors.package.not_found',
  'package.in_use': 'errors.package.in_use',
};

export const PACKAGE_FALLBACK_ERROR_KEY = 'errors.package.update_failed';

export function roundToCents(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export function deriveServiceGrosses(
  rows: ReadonlyArray<PackageServiceWeightRow>,
  price: number
): DerivedServiceGross[] {
  const safePrice = Number.isFinite(price) && price > 0 ? price : 0;
  const totalWeight = rows.reduce(
    (sum, row) => sum + (row.weight > 0 ? row.weight : 0),
    0
  );

  if (rows.length === 0 || totalWeight <= 0 || safePrice <= 0) {
    return rows.map((row) => ({
      id: row.id,
      name: row.name,
      weight: row.weight,
      gross: 0,
    }));
  }

  let allocated = 0;
  return rows.map((row, index) => {
    const isLast = index === rows.length - 1;
    const weight = row.weight > 0 ? row.weight : 0;
    const gross = isLast
      ? roundToCents(safePrice - allocated)
      : roundToCents((weight / totalWeight) * safePrice);
    allocated = roundToCents(allocated + gross);
    return { id: row.id, name: row.name, weight: row.weight, gross };
  });
}
