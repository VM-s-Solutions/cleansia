import Foundation

extension L10n.Orders {
    // Lifecycle actions (detail footer)

    static var slideToTake: String {
        L10n.localized("slide_to_take")
    }

    static var takingOrder: String {
        L10n.localized("taking_order")
    }

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
}
