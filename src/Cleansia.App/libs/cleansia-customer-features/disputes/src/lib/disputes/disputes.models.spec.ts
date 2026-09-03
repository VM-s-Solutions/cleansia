import {
  DisputeListItem,
  DisputeMessageDto,
  DisputeReason,
} from '@cleansia/customer-services';
import {
  CustomerDisputeStatus,
  DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES,
  DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES,
  getDisputeReasonLabelKey,
  getDisputeStatusSeverity,
  hasUnreadStaffReply,
  readCreatedDisputeId,
  isDisputeOpen,
  latestStaffMessageTimestamp,
  validateEvidenceFile,
} from './disputes.models';

describe('disputes.models', () => {
  describe('validateEvidenceFile', () => {
    it.each(DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES)(
      'accepts %s within the size limit',
      (type) => {
        expect(validateEvidenceFile({ type, size: 1024 })).toBeNull();
      }
    );

    it('pins the backend whitelist (UploadDisputeEvidence)', () => {
      expect(DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES).toEqual([
        'image/jpeg',
        'image/jpg',
        'image/png',
        'image/webp',
        'application/pdf',
      ]);
      expect(DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES).toBe(10 * 1024 * 1024);
    });

    it('rejects an out-of-whitelist MIME type', () => {
      expect(validateEvidenceFile({ type: 'image/gif', size: 10 })).toBe(
        'invalid_type'
      );
      expect(validateEvidenceFile({ type: 'application/zip', size: 10 })).toBe(
        'invalid_type'
      );
    });

    it('is case-insensitive on the MIME type like the backend validator', () => {
      expect(validateEvidenceFile({ type: 'IMAGE/PNG', size: 10 })).toBeNull();
    });

    it('rejects a file over 10 MB', () => {
      expect(
        validateEvidenceFile({
          type: 'image/png',
          size: DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES + 1,
        })
      ).toBe('too_large');
    });

    it('accepts a file at exactly the limit', () => {
      expect(
        validateEvidenceFile({
          type: 'image/png',
          size: DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES,
        })
      ).toBeNull();
    });
  });

  describe('getDisputeReasonLabelKey', () => {
    it('maps every generated DisputeReason member', () => {
      expect(getDisputeReasonLabelKey(DisputeReason.QualityIssue)).toBe(
        'pages.disputes.reasons.quality_issue'
      );
      expect(getDisputeReasonLabelKey(DisputeReason.Other)).toBe(
        'pages.disputes.reasons.other'
      );
    });

    it('maps the Chargeback value (8) missing from the generated enum', () => {
      expect(getDisputeReasonLabelKey(8)).toBe(
        'pages.disputes.reasons.chargeback'
      );
    });

    it('falls back safely for unknown or missing values', () => {
      expect(getDisputeReasonLabelKey(99)).toBe(
        'pages.disputes.reasons.unknown'
      );
      expect(getDisputeReasonLabelKey(undefined)).toBe(
        'pages.disputes.reasons.unknown'
      );
    });
  });

  describe('status helpers', () => {
    it('maps statuses to tag severities', () => {
      expect(getDisputeStatusSeverity(CustomerDisputeStatus.Pending)).toBe('warn');
      expect(getDisputeStatusSeverity(CustomerDisputeStatus.UnderReview)).toBe('info');
      expect(getDisputeStatusSeverity(CustomerDisputeStatus.Resolved)).toBe('success');
      expect(getDisputeStatusSeverity(CustomerDisputeStatus.Closed)).toBe('secondary');
      expect(getDisputeStatusSeverity(CustomerDisputeStatus.Escalated)).toBe('danger');
      expect(getDisputeStatusSeverity(undefined)).toBe('info');
    });

    it('treats only Resolved and Closed as terminal for evidence upload', () => {
      expect(isDisputeOpen(CustomerDisputeStatus.Pending)).toBe(true);
      expect(isDisputeOpen(CustomerDisputeStatus.WaitingForResponse)).toBe(true);
      expect(isDisputeOpen(CustomerDisputeStatus.Escalated)).toBe(true);
      expect(isDisputeOpen(CustomerDisputeStatus.Resolved)).toBe(false);
      expect(isDisputeOpen(CustomerDisputeStatus.Closed)).toBe(false);
      expect(isDisputeOpen(undefined)).toBe(false);
    });
  });

  // Owner, 2026-09-03: a photo attached while filing a dispute never arrived.
  // `Dispute/Create` answered with an EMPTY 200 body, so there was no id to
  // upload the evidence against and the file was dropped in silence. These pin
  // both wire shapes, because the generated client keeps the old signature
  // until it is regenerated.
  describe('readCreatedDisputeId', () => {
    it('reads the id off the object body the endpoint returns now', () => {
      expect(readCreatedDisputeId({ disputeId: 'dispute-1' })).toBe('dispute-1');
    });

    it('still reads a bare string, which is what the client is typed for', () => {
      expect(readCreatedDisputeId('dispute-1')).toBe('dispute-1');
    });

    it('answers null for the EMPTY body that hid this bug', () => {
      // Every shape an empty 200 can arrive as through the generated client.
      expect(readCreatedDisputeId(null)).toBeNull();
      expect(readCreatedDisputeId(undefined)).toBeNull();
      expect(readCreatedDisputeId('')).toBeNull();
    });

    it('answers null rather than an empty id, which would upload against nothing', () => {
      expect(readCreatedDisputeId({ disputeId: '' })).toBeNull();
      expect(readCreatedDisputeId({ disputeId: 42 })).toBeNull();
      expect(readCreatedDisputeId({})).toBeNull();
    });
  });

  describe('unread staff reply computation', () => {
    const staffAt = (iso: string) =>
      DisputeMessageDto.fromJS({
        id: 'm1',
        message: 'hi',
        isStaffMessage: true,
        createdOn: iso,
      });
    const customerAt = (iso: string) =>
      DisputeMessageDto.fromJS({
        id: 'm2',
        message: 'hello',
        isStaffMessage: false,
        createdOn: iso,
      });

    it('returns the newest staff message timestamp only', () => {
      const ts = latestStaffMessageTimestamp([
        customerAt('2026-06-09T12:00:00Z'),
        staffAt('2026-06-08T10:00:00Z'),
        staffAt('2026-06-09T08:00:00Z'),
      ]);
      expect(ts).toBe(new Date('2026-06-09T08:00:00Z').toISOString());
    });

    it('returns undefined without staff messages', () => {
      expect(latestStaffMessageTimestamp(undefined)).toBeUndefined();
      expect(
        latestStaffMessageTimestamp([customerAt('2026-06-09T12:00:00Z')])
      ).toBeUndefined();
    });

    it('flags a staff message newer than the last view', () => {
      expect(
        hasUnreadStaffReply('2026-06-09T10:00:00Z', '2026-06-09T08:00:00Z')
      ).toBe(true);
    });

    it('flags a staff message when the dispute was never viewed', () => {
      expect(hasUnreadStaffReply('2026-06-09T10:00:00Z', undefined)).toBe(true);
    });

    it('clears once viewed after the staff message', () => {
      expect(
        hasUnreadStaffReply('2026-06-09T10:00:00Z', '2026-06-09T11:00:00Z')
      ).toBe(false);
    });

    it('never flags without any known staff message', () => {
      expect(hasUnreadStaffReply(undefined, undefined)).toBe(false);
      expect(hasUnreadStaffReply(undefined, '2026-06-09T11:00:00Z')).toBe(false);
    });
  });
});
