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

    enum Invoices {
        static var title: String {
            localized("invoices")
        }

        static var summaryLabel: String {
            localized("invoices_summary_label")
        }

        static func summaryCount(_ count: Int) -> String {
            format("invoices_summary_count", count)
        }

        static var empty: String {
            localized("no_invoices")
        }

        static var cardTotal: String {
            localized("invoice_card_total")
        }

        static func cardJobsCount(_ count: Int) -> String {
            format("invoice_card_jobs_count", count)
        }

        static func cardPaidOn(_ date: String) -> String {
            format("invoice_card_paid_on", date)
        }

        static func cardGeneratedOn(_ date: String) -> String {
            format("invoice_card_generated_on", date)
        }

        static var details: String {
            localized("invoice_details")
        }

        static var heroTotal: String {
            localized("invoice_hero_total")
        }

        static var breakdownSection: String {
            localized("invoice_breakdown_section")
        }

        static var subtotal: String {
            localized("subtotal")
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

        static var periodLabel: String {
            localized("invoice_period_label")
        }

        static func periodJobs(_ count: Int) -> String {
            format("invoice_period_jobs", count)
        }

        static var periodGenerated: String {
            localized("invoice_period_generated")
        }

        static var periodApproved: String {
            localized("invoice_period_approved")
        }

        static var periodPaid: String {
            localized("invoice_period_paid")
        }

        static var viewPeriodPay: String {
            localized("invoice_view_period_pay")
        }

        static var referencesSection: String {
            localized("invoice_references_section")
        }

        static var fieldInvoiceNumber: String {
            localized("invoice_field_invoice_number")
        }

        static var fieldVariableSymbol: String {
            localized("invoice_field_variable_symbol")
        }

        static var fieldPaymentReference: String {
            localized("invoice_field_payment_reference")
        }

        static var fieldCopy: String {
            localized("invoice_field_copy")
        }

        static var fieldCopied: String {
            localized("invoice_field_copied")
        }

        static var notes: String {
            localized("invoice_notes")
        }

        static var notesAdmin: String {
            localized("invoice_notes_admin")
        }

        static var notesBank: String {
            localized("invoice_notes_bank")
        }

        static var openPdf: String {
            localized("open_invoice_pdf")
        }

        static var statusPending: String {
            localized("invoice_status_pending")
        }

        static var statusApproved: String {
            localized("invoice_status_approved")
        }

        static var statusPaid: String {
            localized("invoice_status_paid")
        }

        static var statusDisputed: String {
            localized("invoice_status_disputed")
        }

        static var statusRejected: String {
            localized("invoice_status_rejected")
        }

        static var statusCancelled: String {
            localized("invoice_status_cancelled")
        }
    }
}
