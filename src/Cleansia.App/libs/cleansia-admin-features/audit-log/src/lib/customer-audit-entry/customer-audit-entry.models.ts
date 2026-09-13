import { AuditFieldDiff, buildFieldDiff, parseSnapshot } from '../audit-entry/audit-entry.models';

export interface PayloadRow {
  key: string;
  value: string | null;
}

export interface PayloadView {
  rows: PayloadRow[];
  diff: AuditFieldDiff[];
}

const EMPTY_VIEW: PayloadView = { rows: [], diff: [] };

const DIFF_MEMBERS = ['before', 'after'] as const;

export function buildPayloadView(payloadJson: string | undefined): PayloadView {
  const payload = parseSnapshot(payloadJson);
  if (!payload) return EMPTY_VIEW;

  const hasDiff = DIFF_MEMBERS.some((member) => member in payload);
  const diff = hasDiff
    ? buildFieldDiff(serialiseMember(payload['before']), serialiseMember(payload['after']))
    : [];

  const rows: PayloadRow[] = [];
  for (const [key, value] of Object.entries(payload)) {
    if (hasDiff && (DIFF_MEMBERS as readonly string[]).includes(key)) continue;
    flattenInto(rows, key, value);
  }

  return { rows, diff };
}

export function formatPayloadJson(payloadJson: string | undefined): string {
  if (!payloadJson) return '';
  try {
    return JSON.stringify(JSON.parse(payloadJson), null, 2);
  } catch {
    return payloadJson;
  }
}

function serialiseMember(value: unknown): string | undefined {
  return value === null || value === undefined ? undefined : JSON.stringify(value);
}

function flattenInto(rows: PayloadRow[], key: string, value: unknown): void {
  if (isPlainObject(value)) {
    for (const [childKey, childValue] of Object.entries(value)) {
      flattenInto(rows, `${key}.${childKey}`, childValue);
    }
    return;
  }
  rows.push({ key, value: formatScalar(value) });
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function formatScalar(value: unknown): string | null {
  if (value === undefined || value === null) return null;
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return JSON.stringify(value);
}
