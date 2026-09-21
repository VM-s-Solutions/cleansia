export type StatusBadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

export type StatusBadgeKind =
  | 'order'
  | 'payment'
  | 'contract'
  | 'invoice'
  | 'invoicePdf'
  | 'dispute'
  | 'payPeriod'
  | 'company'
  | 'settlement'
  | 'referral'
  | 'document'
  | 'active'
  | 'promo';

/** What the wire carries for a status: the enum number, its name, or the `Code` pair of both. */
export type StatusBadgeValue =
  | number
  | string
  | boolean
  | { value?: number | null; name?: string | null }
  | null
  | undefined;

export interface StatusBadgeMember {
  /** The backend enum's number; `null` for a status the client derives and the wire never numbers. */
  readonly value: number | null;
  readonly tone: StatusBadgeTone;
}

export interface StatusBadgeKindDefinition {
  readonly labelRoot: string;
  readonly members: Readonly<Record<string, StatusBadgeMember>>;
}

export interface ResolvedStatusBadge {
  readonly tone: StatusBadgeTone;
  readonly labelKey: string;
  readonly fallbackLabel: string;
}

const member = (value: number | null, tone: StatusBadgeTone): StatusBadgeMember => ({ value, tone });

/**
 * Every status the admin and partner apps show, keyed by the backend enum member's name, with the
 * one tone each maps to. The numbers mirror the generated clients and the spec pins them there.
 */
export const STATUS_BADGE_KINDS: Readonly<Record<StatusBadgeKind, StatusBadgeKindDefinition>> = {
  order: {
    labelRoot: 'enums.order_status',
    members: {
      New: member(0, 'neutral'),
      Pending: member(1, 'warning'),
      Confirmed: member(2, 'info'),
      OnTheWay: member(3, 'info'),
      InProgress: member(4, 'info'),
      Completed: member(5, 'success'),
      Cancelled: member(6, 'danger'),
    },
  },
  payment: {
    labelRoot: 'enums.payment_status',
    members: {
      Pending: member(1, 'warning'),
      Paid: member(2, 'success'),
      Failed: member(3, 'danger'),
      Refunded: member(4, 'neutral'),
      Disputed: member(5, 'warning'),
      PartiallyRefunded: member(6, 'neutral'),
    },
  },
  contract: {
    labelRoot: 'enums.contract_status',
    members: {
      Pending: member(1, 'warning'),
      Active: member(2, 'info'),
      Terminated: member(3, 'neutral'),
      Approved: member(4, 'success'),
      Rejected: member(5, 'danger'),
    },
  },
  invoice: {
    labelRoot: 'enums.invoice_status',
    members: {
      Pending: member(1, 'warning'),
      Approved: member(2, 'info'),
      Paid: member(3, 'success'),
      Disputed: member(4, 'warning'),
      Rejected: member(5, 'danger'),
      Cancelled: member(6, 'neutral'),
    },
  },
  invoicePdf: {
    labelRoot: 'enums.invoice_pdf_state',
    members: {
      Ready: member(null, 'success'),
      Pending: member(null, 'warning'),
      Failed: member(null, 'danger'),
    },
  },
  dispute: {
    labelRoot: 'enums.dispute_status',
    members: {
      Pending: member(1, 'warning'),
      UnderReview: member(2, 'info'),
      WaitingForResponse: member(3, 'warning'),
      Resolved: member(4, 'success'),
      Closed: member(5, 'neutral'),
      Escalated: member(6, 'danger'),
    },
  },
  payPeriod: {
    labelRoot: 'enums.pay_period_status',
    members: {
      Open: member(1, 'info'),
      Closed: member(2, 'warning'),
      Paid: member(3, 'success'),
    },
  },
  company: {
    labelRoot: 'enums.company_lifecycle_state',
    members: {
      Operating: member(1, 'success'),
      WindingDown: member(2, 'warning'),
      Deactivated: member(3, 'danger'),
      Frozen: member(4, 'info'),
      Archived: member(5, 'neutral'),
    },
  },
  settlement: {
    labelRoot: 'enums.settlement_fact_status',
    members: {
      Blocking: member(null, 'danger'),
      Settled: member(null, 'success'),
      Informational: member(null, 'neutral'),
    },
  },
  referral: {
    labelRoot: 'enums.referral_status',
    members: {
      Accepted: member(1, 'info'),
      Qualified: member(2, 'success'),
      Expired: member(3, 'warning'),
      Reversed: member(4, 'danger'),
    },
  },
  document: {
    labelRoot: 'enums.document_status',
    members: {
      Pending: member(1, 'warning'),
      Approved: member(2, 'success'),
      Rejected: member(3, 'danger'),
    },
  },
  active: {
    labelRoot: 'enums.active_status',
    members: {
      Inactive: member(0, 'neutral'),
      Active: member(1, 'success'),
    },
  },
  promo: {
    labelRoot: 'enums.promo_code_status',
    members: {
      Active: member(null, 'success'),
      Inactive: member(null, 'neutral'),
      Expired: member(null, 'warning'),
    },
  },
};

export function statusMemberKey(memberName: string): string {
  return memberName.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toLowerCase();
}

const canonical = (name: string): string => name.replace(/[\s_-]/g, '').toLowerCase();

function memberByName(definition: StatusBadgeKindDefinition, name: string): string | undefined {
  const wanted = canonical(name);
  return Object.keys(definition.members).find((memberName) => canonical(memberName) === wanted);
}

function memberByValue(definition: StatusBadgeKindDefinition, value: number): string | undefined {
  return Object.keys(definition.members).find(
    (memberName) => definition.members[memberName].value === value,
  );
}

function memberFor(definition: StatusBadgeKindDefinition, value: StatusBadgeValue): string | undefined {
  if (value === null || value === undefined || value === '') return undefined;
  if (typeof value === 'boolean') return memberByValue(definition, value ? 1 : 0);
  if (typeof value === 'number') return memberByValue(definition, value);
  if (typeof value === 'string') return memberByName(definition, value);
  if (typeof value.value === 'number') {
    const byValue = memberByValue(definition, value.value);
    if (byValue) return byValue;
  }
  return value.name ? memberByName(definition, value.name) : undefined;
}

/**
 * The tone and label key for a status. An unknown value still renders, neutral and under its raw
 * name, so a new enum member is visible in the UI before it is catalogued rather than blank.
 */
export function resolveStatusBadge(
  kind: StatusBadgeKind,
  value: StatusBadgeValue,
): ResolvedStatusBadge | null {
  if (value === null || value === undefined || value === '') return null;
  const definition = STATUS_BADGE_KINDS[kind];
  const memberName = memberFor(definition, value);
  if (!memberName) {
    const raw = typeof value === 'object' ? (value.name ?? String(value.value ?? '')) : String(value);
    return { tone: 'neutral', labelKey: '', fallbackLabel: raw };
  }
  return {
    tone: definition.members[memberName].tone,
    labelKey: `${definition.labelRoot}.${statusMemberKey(memberName)}`,
    fallbackLabel: memberName,
  };
}
