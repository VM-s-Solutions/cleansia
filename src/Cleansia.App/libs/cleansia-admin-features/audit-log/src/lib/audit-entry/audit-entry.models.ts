export interface AuditFieldDiff {
  field: string;
  before: string | null;
  after: string | null;
  changed: boolean;
}

export function parseSnapshot(json: string | undefined): Record<string, unknown> | null {
  const trimmed = json?.trim();
  if (!trimmed) return null;
  try {
    const parsed: unknown = JSON.parse(trimmed);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return null;
  } catch {
    return null;
  }
}

export function buildFieldDiff(
  beforeJson: string | undefined,
  afterJson: string | undefined
): AuditFieldDiff[] {
  const before = parseSnapshot(beforeJson);
  const after = parseSnapshot(afterJson);

  if (!before && !after) return [];

  const fields = new Set<string>([
    ...Object.keys(before ?? {}),
    ...Object.keys(after ?? {}),
  ]);

  return [...fields].sort().map((field) => {
    const beforeValue = formatValue(before?.[field]);
    const afterValue = formatValue(after?.[field]);
    return {
      field,
      before: beforeValue,
      after: afterValue,
      changed: beforeValue !== afterValue,
    };
  });
}

function formatValue(value: unknown): string | null {
  if (value === undefined || value === null) return null;
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }
  return JSON.stringify(value);
}
