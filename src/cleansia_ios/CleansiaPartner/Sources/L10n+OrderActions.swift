import Foundation

extension L10n.Orders {
    // Lifecycle actions (detail footer)

    static var notifyOnTheWay: String {
        L10n.localized("notify_on_the_way")
    }

    static var slideToStart: String {
        L10n.localized("slide_to_start")
    }

    static var startingOrder: String {
        L10n.localized("starting_order")
    }

    static var markCashCollected: String {
        L10n.localized("partner_order_mark_cash_collected")
    }

    static var cashCollectedToast: String {
        L10n.localized("cash_collected_toast")
    }

    static var markCashCollectedConfirmTitle: String {
        L10n.localized("partner_order_mark_cash_collected_confirm_title")
    }

    static func markCashCollectedConfirmMessage(_ amount: String) -> String {
        L10n.format("partner_order_mark_cash_collected_confirm_message", amount)
    }

    static var markCashCollectedConfirmMessageNoAmount: String {
        L10n.localized("partner_order_mark_cash_collected_confirm_message_no_amount")
    }

    static var markCashCollectedConfirmAction: String {
        L10n.localized("partner_order_mark_cash_collected_confirm_action")
    }

    static var cashNotPaidAction: String {
        L10n.localized("order_cash_not_paid_action")
    }

    static var cashNotPaidConfirmTitle: String {
        L10n.localized("order_cash_not_paid_confirm_title")
    }

    static func cashNotPaidConfirmMessage(_ amount: String) -> String {
        L10n.format("order_cash_not_paid_confirm_message", amount)
    }

    static var cashNotPaidConfirmMessageNoAmount: String {
        L10n.localized("order_cash_not_paid_confirm_message_no_amount")
    }

    static var cashNotPaidConfirmAction: String {
        L10n.localized("order_cash_not_paid_confirm_action")
    }

    static var cashNotPaidReportedToast: String {
        L10n.localized("order_cash_not_paid_reported_toast")
    }

    static var slideToComplete: String {
        L10n.localized("slide_to_complete")
    }

    static var completingOrder: String {
        L10n.localized("completing_order")
    }

    static var orderCompletedToast: String {
        L10n.localized("order_completed_toast")
    }

    static var orderStartedToast: String {
        L10n.localized("order_started")
    }

    static var afterPhotosRequired: String {
        L10n.localized("error_key_order_after_photos_required")
    }

    static var completeBlockedCashSequence: String {
        L10n.localized("partner_order_complete_blocked_cash_sequence")
    }

    // Lifecycle actions (Active-row swipe)

    static var swipeToNotifyOnTheWay: String {
        L10n.localized("swipe_to_notify_on_the_way")
    }

    static var customerNotifiedOnTheWay: String {
        L10n.localized("customer_notified_on_the_way")
    }

    static var swipeToStart: String {
        L10n.localized("swipe_to_start")
    }

    static var swipeToComplete: String {
        L10n.localized("swipe_to_complete")
    }

    // Shared action labels

    static var save: String {
        L10n.localized("save")
    }

    static var delete: String {
        L10n.localized("delete")
    }

    // Checklist

    static var checklistSectionTitle: String {
        L10n.localized("checklist_section_title")
    }

    static var checklistServicesLabel: String {
        L10n.localized("checklist_services_label")
    }

    static var checklistPackagesLabel: String {
        L10n.localized("checklist_packages_label")
    }

    static var checklistExtrasLabel: String {
        L10n.localized("checklist_extras_label")
    }

    static var checklistLockedHint: String {
        L10n.localized("checklist_locked_hint")
    }

    static var checklistAllDoneHint: String {
        L10n.localized("checklist_all_done_hint")
    }

    static func checklistProgress(_ done: Int, _ total: Int) -> String {
        L10n.format("checklist_progress", done, total)
    }

    // Notes & issues

    static var notesAndIssues: String {
        L10n.localized("notes_and_issues")
    }

    static var addNote: String {
        L10n.localized("add_note")
    }

    static var addNoteDesc: String {
        L10n.localized("add_note_desc")
    }

    static var noteContent: String {
        L10n.localized("note_content")
    }

    static var editNote: String {
        L10n.localized("edit_note")
    }

    static var deleteNoteConfirm: String {
        L10n.localized("delete_note_confirm")
    }

    static var reportIssue: String {
        L10n.localized("report_issue")
    }

    static var reportIssueDesc: String {
        L10n.localized("report_issue_desc")
    }

    static var issueDescription: String {
        L10n.localized("issue_description")
    }

    static var editIssue: String {
        L10n.localized("edit_issue")
    }

    static var deleteIssueConfirm: String {
        L10n.localized("delete_issue_confirm")
    }

    static var noteSavedToast: String {
        L10n.localized("note_saved_toast")
    }

    static var noteDeletedToast: String {
        L10n.localized("note_deleted_toast")
    }

    static var issueReportedToast: String {
        L10n.localized("issue_reported_toast")
    }

    static var issueUpdatedToast: String {
        L10n.localized("issue_updated_toast")
    }

    static var issueDeletedToast: String {
        L10n.localized("issue_deleted_toast")
    }

    // Status timeline

    static var statusTimelineSectionTitle: String {
        L10n.localized("status_timeline_section_title")
    }

    // Cannot get in

    static var lockoutCardTitle: String {
        L10n.localized("lockout_card_title")
    }

    static func lockoutCardNotYet(_ time: String, _ minutes: Int) -> String {
        L10n.format("lockout_card_not_yet", time, minutes)
    }

    static var lockoutCardBody: String {
        L10n.localized("lockout_card_body")
    }

    static var lockoutEntrancePhoto: String {
        L10n.localized("lockout_entrance_photo")
    }

    static var lockoutPhotoNeeded: String {
        L10n.localized("lockout_photo_needed")
    }

    static var lockoutReportAction: String {
        L10n.localized("lockout_report_action")
    }

    static var lockoutSheetTitle: String {
        L10n.localized("lockout_sheet_title")
    }

    static var lockoutSheetDescription: String {
        L10n.localized("lockout_sheet_description")
    }

    static var lockoutCallAttemptsLabel: String {
        L10n.localized("lockout_call_attempts_label")
    }

    static var lockoutReportedToast: String {
        L10n.localized("lockout_reported_toast")
    }

    static var lockoutReportedTitle: String {
        L10n.localized("lockout_reported_title")
    }

    static func lockoutReportedBody(_ time: String) -> String {
        L10n.format("lockout_reported_body", time)
    }

    static func lockoutReportedCalls(_ calls: String) -> String {
        L10n.format("lockout_reported_calls", calls)
    }
}
