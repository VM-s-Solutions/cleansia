import {
  CreateDisputeResponse,
  DisputeMessageDto,
  DisputeReason,
} from '@cleansia/customer-services';
import { TagSeverity } from '@cleansia/types';

/**
 * One item of an order a customer can point at when filing a dispute.
 *
 * The identity is the pair the server uses — `(serviceId, packageId?)`. A standalone service leaves
 * `packageId` null; a service that came inside a bundle carries both, because "the oven clean in the
 * Deep Clean package" and "the oven clean I bought on its own" are different lines on the same order
 * and an admin refunding one must not refund the other.
 * -> CreateDispute.DisputeLineSelection
 */
export interface DisputeLineSelection {
  serviceId: string;
  packageId: string | null;
}

/** A selectable line, with the label the customer reads and a key the template can track by. */
export interface DisputeLineOption extends DisputeLineSelection {
  key: string;
  label: string;
  /** The bundle this item came in, so the list can say "Oven clean — in Deep Clean". */
  packageLabel: string | null;
}

/** Stable within one order: the server's own identity, flattened. */
export function disputeLineKey(line: DisputeLineSelection): string {
  return `${line.packageId ?? ''}|${line.serviceId}`;
}

// Mirrors the backend DisputeStatus enum — the generated customer client does
// not expose it (no customer endpoint takes it as a typed parameter yet).
export enum CustomerDisputeStatus {
  Pending = 1,
  UnderReview = 2,
  WaitingForResponse = 3,
  Resolved = 4,
  Closed = 5,
  Escalated = 6,
}

export const DISPUTE_STATUS_LABEL_KEYS: Record<CustomerDisputeStatus, string> =
  {
    [CustomerDisputeStatus.Pending]: 'pages.disputes.statuses.pending',
    [CustomerDisputeStatus.UnderReview]: 'pages.disputes.statuses.under_review',
    [CustomerDisputeStatus.WaitingForResponse]:
      'pages.disputes.statuses.waiting_for_response',
    [CustomerDisputeStatus.Resolved]: 'pages.disputes.statuses.resolved',
    [CustomerDisputeStatus.Closed]: 'pages.disputes.statuses.closed',
    [CustomerDisputeStatus.Escalated]: 'pages.disputes.statuses.escalated',
  };

export function getDisputeStatusSeverity(
  statusValue: number | undefined
): TagSeverity {
  switch (statusValue) {
    case CustomerDisputeStatus.Pending:
      return 'warn';
    case CustomerDisputeStatus.UnderReview:
      return 'info';
    case CustomerDisputeStatus.WaitingForResponse:
      return 'warn';
    case CustomerDisputeStatus.Resolved:
      return 'success';
    case CustomerDisputeStatus.Closed:
      return 'secondary';
    case CustomerDisputeStatus.Escalated:
      return 'danger';
    default:
      return 'info';
  }
}

export function isDisputeOpen(statusValue: number | undefined): boolean {
  return (
    statusValue != null &&
    statusValue !== CustomerDisputeStatus.Resolved &&
    statusValue !== CustomerDisputeStatus.Closed
  );
}

// Mirrors UploadDisputeEvidence.AllowedContentTypes / MaxFileSizeBytes.
export const DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES: readonly string[] = [
  'image/jpeg',
  'image/jpg',
  'image/png',
  'image/webp',
  'application/pdf',
];

export const DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024;

export type EvidenceFileError = 'invalid_type' | 'too_large';

export function validateEvidenceFile(file: {
  type: string;
  size: number;
}): EvidenceFileError | null {
  if (
    !DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES.includes(file.type.toLowerCase())
  ) {
    return 'invalid_type';
  }
  if (file.size > DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES) {
    return 'too_large';
  }
  return null;
}

// Chargeback (=8) exists on the backend but is missing from the generated
// DisputeReason until the customer client is regenerated — map it explicitly
// and fall back safely for any other unknown value.
const DISPUTE_REASON_LABEL_KEYS: Record<number, string> = {
  [DisputeReason.QualityIssue]: 'pages.disputes.reasons.quality_issue',
  [DisputeReason.ServiceNotProvided]:
    'pages.disputes.reasons.service_not_provided',
  [DisputeReason.ServiceIncomplete]:
    'pages.disputes.reasons.service_incomplete',
  [DisputeReason.DamagedProperty]: 'pages.disputes.reasons.damaged_property',
  [DisputeReason.UnauthorizedCharge]:
    'pages.disputes.reasons.unauthorized_charge',
  [DisputeReason.IncorrectAmount]: 'pages.disputes.reasons.incorrect_amount',
  [DisputeReason.Other]: 'pages.disputes.reasons.other',
  8: 'pages.disputes.reasons.chargeback',
};

export function getDisputeReasonLabelKey(
  reasonValue: number | undefined
): string {
  if (reasonValue != null && DISPUTE_REASON_LABEL_KEYS[reasonValue]) {
    return DISPUTE_REASON_LABEL_KEYS[reasonValue];
  }
  return 'pages.disputes.reasons.unknown';
}

export function latestStaffMessageTimestamp(
  messages: DisputeMessageDto[] | undefined
): string | undefined {
  const staffTimes = (messages ?? [])
    .filter((m) => m.isStaffMessage && m.createdOn)
    .map((m) => new Date(m.createdOn).getTime());
  if (staffTimes.length === 0) return undefined;
  return new Date(Math.max(...staffTimes)).toISOString();
}

export function hasUnreadStaffReply(
  latestStaffMessageOn: string | undefined,
  lastViewedOn: string | undefined
): boolean {
  if (!latestStaffMessageOn) return false;
  if (!lastViewedOn) return true;
  return (
    new Date(latestStaffMessageOn).getTime() >
    new Date(lastViewedOn).getTime()
  );
}

export const DISPUTE_UPLOAD_ERROR_KEY_MAP: Record<string, string> = {
  'dispute.not_found': 'api.dispute.not_found',
  'dispute.not_owned_by_user': 'api.dispute.not_owned_by_user',
  'file.invalid_file_type': 'api.file.invalid_file_type',
  'file.size_exceeded': 'api.file.size_exceeded',
  'file.required': 'api.file.required',
};

export const DISPUTE_UPLOAD_FALLBACK_ERROR_KEY =
  'pages.disputes.evidence.upload_error';

/**
 * The id of a dispute that was just created, or null when the server did not
 * send one.
 *
 * `Dispute/Create` used to answer with an EMPTY 200 body — `HandleResult<string>`
 * never matched a `BusinessResult<CreateDispute.Response>` and fell through to
 * `Ok()` — so a caller had no id to attach the customer's photo to and the file
 * was dropped in silence. It returns `{ disputeId }` now.
 *
 * The check that remains is not paranoia about the shape: `disputeId` is typed
 * `string | undefined`, and uploading a photo against an empty id would put it
 * nowhere just as quietly as before.
 */
export function readCreatedDisputeId(
  response: CreateDisputeResponse | null | undefined,
): string | null {
  const id = response?.disputeId;
  return typeof id === 'string' && id.length > 0 ? id : null;
}
