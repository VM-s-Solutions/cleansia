import Foundation

extension L10n {
    enum Earnings {
        static var title: String {
            localized("earnings_title")
        }

        static var currentPeriod: String {
            localized("earnings_current_period")
        }

        static var estimateHelper: String {
            localized("earnings_estimate_helper")
        }

        static var today: String {
            localized("earnings_today")
        }

        static var thisWeek: String {
            localized("earnings_this_week")
        }

        static var lastMonth: String {
            localized("earnings_last_month")
        }

        static func jobsDoneCount(_ count: Int) -> String {
            format("earnings_jobs_done_count", count)
        }

        static var payPeriod: String {
            localized("earnings_pay_period")
        }

        static func daysRemaining(_ count: Int) -> String {
            format("earnings_days_remaining", count)
        }

        static var nextPayout: String {
            localized("earnings_next_payout")
        }

        static var viewInvoices: String {
            localized("earnings_view_invoices")
        }

        static var viewInvoicesSubtitle: String {
            localized("earnings_view_invoices_subtitle")
        }
    }

    enum PeriodPay {
        static var title: String {
            localized("period_pay_title")
        }

        static var heroLabel: String {
            localized("period_pay_hero_label")
        }

        static func jobsCount(_ count: Int) -> String {
            format("period_pay_jobs_count", count)
        }

        static var breakdownSection: String {
            localized("period_pay_breakdown_section")
        }

        static var base: String {
            localized("period_pay_base")
        }

        static var extras: String {
            localized("period_pay_extras")
        }

        static var expenses: String {
            localized("period_pay_expenses")
        }

        static var bonus: String {
            localized("bonus")
        }

        static var deductions: String {
            localized("deductions")
        }

        static var total: String {
            localized("total")
        }

        static var jobsSection: String {
            localized("period_pay_jobs_section")
        }

        static var empty: String {
            localized("period_pay_empty")
        }

        static var error: String {
            localized("period_pay_error")
        }
    }
}
