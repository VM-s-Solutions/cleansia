-- ============================================================
-- CLEANSIA DISPUTES SEED DATA
-- ============================================================
-- This script inserts sample dispute records for testing
-- Run this after the main insert_seed_data.sql script
-- ============================================================

BEGIN TRANSACTION;

-- ============================================================
-- DISPUTES
-- ============================================================
-- DisputeStatus: Pending=1, UnderReview=2, WaitingForResponse=3, Resolved=4, Closed=5, Escalated=6
-- DisputeReason: QualityIssue=1, ServiceNotProvided=2, ServiceIncomplete=3, DamagedProperty=4, UnauthorizedCharge=5, IncorrectAmount=6, Other=7
INSERT INTO public."Disputes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn",
    "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
    "OrderId", "UserId", "Status", "Reason", "Description",
    "RefundAmount", "ResolutionNotes", "ResolvedOn"
)
VALUES
-- Dispute 1: Resolved quality issue (Order 1)
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '15 days',
 'system', CURRENT_TIMESTAMP - INTERVAL '10 days', NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0001' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0001' LIMIT 1),
 4, -- Resolved
 1, -- QualityIssue
 'The cleaning was not thorough. Several areas in the bathroom were missed, including behind the toilet and under the sink. The kitchen countertops still had visible stains after the cleaning crew left.',
 500.00,
 'Apologized to the customer and offered a partial refund of 500 CZK. Scheduled a follow-up cleaning at no additional cost. The employee responsible has been retrained on bathroom cleaning procedures.',
 CURRENT_TIMESTAMP - INTERVAL '10 days'),

-- Dispute 2: Service incomplete - waiting for response (Order 2)
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '5 days',
 'system', CURRENT_TIMESTAMP - INTERVAL '3 days', NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0002' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0002' LIMIT 1),
 3, -- WaitingForResponse
 3, -- ServiceIncomplete
 'The cleaning service was supposed to include window cleaning, but the windows were not cleaned at all. I paid extra for this service and I expect it to be completed or refunded.',
 NULL,
 NULL,
 NULL),

-- Dispute 3: Damaged property - under review (Order 3)
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '3 days',
 'system', CURRENT_TIMESTAMP - INTERVAL '2 days', NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0003' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0003' LIMIT 1),
 2, -- UnderReview
 4, -- DamagedProperty
 'During the cleaning, a vase was knocked over and broken. This was an expensive decorative piece that has sentimental value. I would like compensation for the damage.',
 1200.00,
 NULL,
 NULL),

-- Dispute 4: Unauthorized charge - pending (Order 4)
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '2 days',
 NULL, NULL, NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0004' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0004' LIMIT 1),
 1, -- Pending
 5, -- UnauthorizedCharge
 'I was charged 2799 CZK but the estimate I received was for 2299 CZK. The extra charge was not explained or authorized by me. Please explain this discrepancy.',
 NULL,
 NULL,
 NULL),

-- Dispute 5: Service not provided - escalated (Order 5)
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '7 days',
 'system', CURRENT_TIMESTAMP - INTERVAL '1 day', NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0005' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0005' LIMIT 1),
 6, -- Escalated
 2, -- ServiceNotProvided
 'The cleaning crew never showed up for the scheduled appointment. I took time off work specifically for this and received no communication about cancellation or rescheduling. This is completely unacceptable.',
 3999.00,
 NULL,
 NULL),

-- Dispute 6: Incorrect amount - resolved (Order 6)
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '20 days',
 'system', CURRENT_TIMESTAMP - INTERVAL '18 days', NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0006' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0006' LIMIT 1),
 4, -- Resolved
 6, -- IncorrectAmount
 'I was charged twice for the same service. My credit card shows two transactions of 1299 CZK on the same day for order CLS-2026-0006.',
 1299.00,
 'Verified the duplicate charge was a payment processing error. Issued full refund of 1299 CZK for the duplicate transaction. Customer confirmed receipt of refund.',
 CURRENT_TIMESTAMP - INTERVAL '18 days'),

-- Dispute 7: Other reason for order 1 - under review
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '1 day',
 NULL, NULL, NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0001' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0001' LIMIT 1),
 2, -- UnderReview
 7, -- Other
 'The cleaning crew arrived 2 hours late without any notification. This caused me to miss an important meeting. While the cleaning quality was acceptable, the lack of communication and punctuality is a serious concern.',
 NULL,
 NULL,
 NULL),

-- Dispute 8: Quality issue for order 13 - pending
(generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '12 hours',
 NULL, NULL, NULL, NULL,
 (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0013' LIMIT 1),
 (SELECT "UserId" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2026-0013' LIMIT 1),
 1, -- Pending
 1, -- QualityIssue
 'The kitchen was left in a worse state than before. Cleaning products were not properly rinsed from surfaces, leaving a sticky residue on the countertops and cabinets.',
 NULL,
 NULL,
 NULL);

COMMIT;

-- ============================================================
-- VERIFICATION QUERY
-- ============================================================
-- Run this to verify disputes were inserted correctly
SELECT
    d."Id",
    o."DisplayOrderNumber" as "OrderNumber",
    d."Status" as "StatusCode",
    d."Reason" as "ReasonCode",
    LEFT(d."Description", 50) as "DescriptionPreview",
    d."RefundAmount",
    d."CreatedOn",
    d."ResolvedOn"
FROM public."Disputes" d
INNER JOIN public."Orders" o ON d."OrderId" = o."Id"
ORDER BY d."CreatedOn" DESC;
