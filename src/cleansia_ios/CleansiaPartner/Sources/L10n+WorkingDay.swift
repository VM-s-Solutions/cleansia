import CleansiaCore
import Foundation

// MARK: - The working day: day panels, the status line, the week strip, the cash meter and History's month

extension L10n.Orders {
    /// "your job 15:00–18:00", or with two or more "your jobs 08:00–10:00, 14:00–16:00".
    static func dayYourJob(_ windows: String, count: Int) -> String {
        L10n.format(count > 1 ? "orders_day_your_jobs" : "orders_day_your_job", windows)
    }

    /// The header's short form, still a word: "yours 14:00–16:00".
    static func dayYoursShort(_ windows: String, count: Int) -> String {
        L10n.format(count > 1 ? "orders_day_yours_short_many" : "orders_day_yours_short", windows)
    }

    static func weekOpenCount(_ count: Int) -> String {
        L10n.plural("active_week_open_count", count)
    }

    static func overlapDay(_ count: Int, day: String, window: String) -> String {
        L10n.plural("orders_overlap_day", count, day, window)
    }

    static func cashHiddenBack(_ limit: String) -> String {
        L10n.format("cash_hidden_back", limit)
    }

    static func cashLimitAt(_ limit: String) -> String {
        L10n.format("cash_limit_at", limit)
    }

    static var underWayShort: String {
        L10n.localized("orders_flag_under_way_short")
    }

    static func flagCashCollect(_ amount: String) -> String {
        L10n.format("orders_flag_cash_collect", amount)
    }

    static func cashFlag(_ amount: String, collected: Bool) -> String {
        collected ? historyCashCollected(amount) : flagCashCollect(amount)
    }

    static func checkedAt(_ time: String) -> String {
        L10n.format("available_checked_at", time)
    }

    static var yourNextJobs: String {
        L10n.localized("orders_your_next_jobs")
    }

    static var cashHeldTitle: String {
        L10n.localized("cash_held_title")
    }

    static func cashHeldOfLimit(_ held: String, _ limit: String) -> String {
        L10n.format("cash_held_of_limit", held, limit)
    }

    static var cashHeldHint: String {
        L10n.localized("cash_held_hint")
    }

    static var activeEmptyBody: String {
        L10n.localized("active_empty_body")
    }

    static var nothingElse: String {
        L10n.localized("active_nothing_else")
    }

    static var openNext7Days: String {
        L10n.localized("active_open_next_7_days")
    }

    static var weekLegendYourJob: String {
        L10n.localized("active_week_legend_your_job")
    }

    static var soonestOpen: String {
        L10n.localized("active_soonest_open")
    }

    static var seeOffer: String {
        L10n.localized("active_see_offer")
    }

    static func seeOpen(_ count: Int) -> String {
        L10n.plural("active_see_open", count)
    }

    static func historyCashCollected(_ amount: String) -> String {
        L10n.format("history_cash_collected", amount)
    }

    static func historyCashNotPaid(_ amount: String) -> String {
        L10n.format("history_cash_not_paid", amount)
    }

    static var historyPaidInFull: String {
        L10n.localized("history_paid_in_full")
    }

    static var historyEmptyMonth: String {
        L10n.localized("history_empty_month")
    }

    static var historyEmptyWeek: String {
        L10n.localized("history_empty_week")
    }

    static var historyEmptyBody: String {
        L10n.localized("history_empty_body")
    }

    static var historyLastMonth: String {
        L10n.localized("history_last_month")
    }

    static func historyShowPeriod(_ period: String) -> String {
        L10n.format("history_show_period", period)
    }

    static var runStepSetOff: String {
        L10n.localized("run_step_set_off")
    }

    static var runStepGetIn: String {
        L10n.localized("run_step_get_in")
    }

    static var runStepClean: String {
        L10n.localized("run_step_clean")
    }

    static var runStepFinish: String {
        L10n.localized("run_step_finish")
    }

    static func inDays(_ count: Int) -> String {
        L10n.plural("detail_in_days", count)
    }

    /// "Step 3 / 4".
    static func stepCounter(_ step: Int, _ count: Int) -> String {
        L10n.format("tracker_step_counter", step, count)
    }

    static var customerSectionTitle: String {
        L10n.localized("customer")
    }

    static var scopeSectionTitle: String {
        L10n.localized("scope_section_title")
    }

    static var propertyLabel: String {
        L10n.localized("property")
    }

    static var contractAcceptedLabel: String {
        L10n.localized("contract_accepted_label")
    }

    static var contractVersionLabel: String {
        L10n.localized("contract_version_label")
    }

    static func left(_ duration: String) -> String {
        L10n.format("detail_left", duration)
    }

    static func finishedAt(_ when: String) -> String {
        L10n.format("detail_finished_at", when)
    }

    static func cancelledAt(_ time: String) -> String {
        L10n.format("detail_cancelled_at", time)
    }

    static var setOffNow: String {
        L10n.localized("status_set_off_now")
    }

    static var waitingCompany: String {
        L10n.localized("detail_waiting_company")
    }

    static func reportedAt(_ time: String) -> String {
        L10n.format("detail_reported_at", time)
    }

    /// Why the system cancelled a job, for the five reasons the server writes; any other key says nothing.
    static func cancellationReason(_ key: String?) -> String? {
        guard let key, cancellationReasons.contains(key) else { return nil }
        return L10n.localized(key)
    }

    static let cancellationReasons: Set = [
        "order.cancelled.customer_lockout",
        "order.cancelled.recurring_not_confirmed",
        "order.cancelled.payment_not_completed",
        "order.cancelled.no_cleaner_available",
        "order.cancelled.company_wind_down"
    ]
}

// MARK: - The job detail

extension L10n.Orders {
    static var cancelLockoutPaid: String {
        L10n.localized("cancel_lockout_paid")
    }

    static var cancelNoFee: String {
        L10n.localized("cancel_no_fee")
    }

    static func jobStamp(_ value: String) -> String {
        L10n.format("run_job_stamp", value)
    }

    static func jobStampSpoken(_ value: String) -> String {
        L10n.format("run_job_stamp_a11y", value)
    }

    static var lateAlreadyThere: String {
        L10n.localized("late_already_there")
    }

    static var crewAllMarked: String {
        L10n.localized("crew_all_marked")
    }

    static func waitingFor(_ value: String) -> String {
        L10n.format("detail_waiting_for", value)
    }

    static var yourJobThatDay: String {
        L10n.localized("detail_your_job_that_day")
    }

    static func mateOnIt(_ value: String) -> String {
        L10n.format("detail_mate_on_it", value)
    }

    static var samePayLate: String {
        L10n.localized("detail_same_pay_late")
    }

    static var runSpoken: String {
        L10n.localized("run_a11y")
    }

    static func stepDoneAt(_ value: String) -> String {
        L10n.format("run_step_done_at", value)
    }

    static func runFrom(_ value: String) -> String {
        L10n.format("run_from", value)
    }

    static func runDayFrom(_ first: String, _ second: String) -> String {
        L10n.format("run_day_from", first, second)
    }

    static func runAt(_ value: String) -> String {
        L10n.format("run_at", value)
    }

    static func runBy(_ value: String) -> String {
        L10n.format("run_by", value)
    }

    static var accessShownOnSetOff: String {
        L10n.localized("access_shown_on_set_off")
    }

    static var lockoutPaidInFull: String {
        L10n.localized("lockout_paid_in_full")
    }

    static func floor(_ value: String) -> String {
        L10n.format("detail_floor", value)
    }

    static func flat(_ value: String) -> String {
        L10n.format("detail_flat", value)
    }

    static var checklistPhoneOnly: String {
        L10n.localized("checklist_phone_only")
    }

    static var photosAdd: String {
        L10n.localized("photos_add")
    }

    static var photosAfterNeeded: String {
        L10n.localized("photos_after_needed")
    }

    static var issueNotResolved: String {
        L10n.localized("issue_not_resolved")
    }

    static var crewYou: String {
        L10n.localized("crew_you")
    }

    static var crewNotYet: String {
        L10n.localized("crew_not_yet")
    }

    static func crewDoneAt(_ value: String) -> String {
        L10n.format("crew_done_at", value)
    }

    static var finishNeedsPhoto: String {
        L10n.localized("finish_needs_photo")
    }

    static var finishNeedsCrew: String {
        L10n.localized("finish_needs_crew")
    }

    static var finishBeforeComplete: String {
        L10n.localized("finish_before_complete")
    }

    static var finishAfterPhoto: String {
        L10n.localized("finish_after_photo")
    }

    static var finishCashCollected: String {
        L10n.localized("finish_cash_collected")
    }

    static var crewBothParts: String {
        L10n.localized("crew_both_parts")
    }

    static func cashRule(_ value: String) -> String {
        L10n.format("cash_rule", value)
    }

    static func cashMarkAmount(_ value: String) -> String {
        L10n.format("cash_mark_amount", value)
    }

    static var markDoneSub: String {
        L10n.localized("crew_mark_done_sub")
    }

    static var takeAfterPhoto: String {
        L10n.localized("take_after_photo")
    }

    static func dropPossibleUntil(_ value: String) -> String {
        L10n.format("drop_possible_until", value)
    }

    static var shownAfterTake: String {
        L10n.localized("detail_shown_after_take")
    }

    static var hiddenAddress: String {
        L10n.localized("detail_hidden_address")
    }

    static var hiddenContact: String {
        L10n.localized("detail_hidden_contact")
    }

    static var hiddenNotes: String {
        L10n.localized("detail_hidden_notes")
    }

    static func detailsCloseTomorrow(_ value: String) -> String {
        L10n.format("details_close_tomorrow", value)
    }

    static func detailsCloseToday(_ value: String) -> String {
        L10n.format("details_close_today", value)
    }

    static var dayLineFree: String {
        L10n.localized("day_line_free")
    }

    static var laterToday: String {
        L10n.localized("detail_later_today")
    }

    static var alsoOpenThatDay: String {
        L10n.localized("detail_also_open_that_day")
    }

    static var openToday: String {
        L10n.localized("detail_open_today")
    }

    static var errorRest: String {
        L10n.localized("detail_error_rest")
    }

    static var errorRestBody: String {
        L10n.localized("detail_error_rest_body")
    }

    static var errorJob: String {
        L10n.localized("detail_error_job")
    }

    static var gone: String {
        L10n.localized("detail_gone")
    }

    static var offSchedule: String {
        L10n.localized("detail_off_schedule")
    }

    static var errorMessage: String {
        L10n.localized("order_detail_error_message")
    }

    static var loading: String {
        L10n.localized("loading")
    }

    static var removalLead: String {
        L10n.localized("order_removal_lead")
    }

    static var browseJobs: String {
        L10n.localized("dash_browse_jobs")
    }

    /// The push's own title for a seat handed over with no removal on record: "Job reassigned".
    static var reassignedTitle: String {
        L10n.localized("push.order.assignment_revoked.title")
    }

    /// `CompleteOrder`'s own refusal of a card Stripe has not charged, from the shared error catalogue.
    static var paymentNotConfirmed: String {
        ApiErrorLocalizer().message(for: ApiError(code: "order.payment_not_confirmed"))
    }

    /// The four ways in the booking wizard offers; any other value says nothing.
    static func accessMode(_ mode: String?) -> String? {
        switch mode {
        case "at_home": L10n.localized("access_mode_at_home")
        case "keys_handover": L10n.localized("access_mode_keys_handover")
        case "door_code": L10n.localized("access_mode_door_code")
        case "reception": L10n.localized("access_mode_reception")
        default: nil
        }
    }
}
