import Foundation

enum L10n {
    static var welcomeBack: String {
        localized("welcome_back")
    }

    static var loginSubtitle: String {
        localized("login_subtitle")
    }

    static var email: String {
        localized("email")
    }

    static var password: String {
        localized("password")
    }

    static var rememberMe: String {
        localized("remember_me")
    }

    static var forgotPassword: String {
        localized("forgot_password")
    }

    static var login: String {
        localized("login")
    }

    static var dontHaveAccount: String {
        localized("dont_have_account")
    }

    static var signUpHere: String {
        localized("sign_up_here")
    }

    static var loginErrorEmailRequired: String {
        localized("login_error_email_required")
    }

    static var loginErrorEmailInvalid: String {
        localized("login_error_email_invalid")
    }

    static var loginErrorPasswordRequired: String {
        localized("login_error_password_required")
    }

    static var retry: String {
        localized("retry")
    }

    enum Dashboard {
        static var goodMorning: String {
            localized("good_morning")
        }

        static var goodAfternoon: String {
            localized("good_afternoon")
        }

        static var goodEvening: String {
            localized("good_evening")
        }

        static func goodMorningName(_ name: String) -> String {
            format("good_morning_name", name)
        }

        static func goodAfternoonName(_ name: String) -> String {
            format("good_afternoon_name", name)
        }

        static func goodEveningName(_ name: String) -> String {
            format("good_evening_name", name)
        }

        static var stateFreeToday: String {
            localized("dash_state_free_today")
        }

        static var stateOneToday: String {
            localized("dash_state_one_today")
        }

        static func stateManyToday(_ count: Int) -> String {
            format("dash_state_many_today", count)
        }

        static var earningsWeek: String {
            localized("dash_earnings_week")
        }

        static var earningsToday: String {
            localized("dash_earnings_today")
        }

        static var noCompletedYet: String {
            localized("dash_no_completed_yet")
        }

        static func jobsDoneCount(_ count: Int) -> String {
            format("dash_jobs_done_count", count)
        }

        static var noJobsTodayShort: String {
            localized("dash_no_jobs_today_short")
        }

        static var avgPerJob: String {
            localized("dash_avg_per_job")
        }

        static var earningsViewDetails: String {
            localized("dash_earnings_view_details")
        }

        static var currentPeriod: String {
            localized("dash_current_period")
        }

        static func payPeriodProgress(_ day: Int, _ total: Int) -> String {
            format("dash_pay_period_progress", day, total)
        }

        static func nextPayout(_ date: String) -> String {
            format("dash_next_payout", date)
        }

        static var lastMonthSection: String {
            localized("dash_last_month_section")
        }

        static var lastMonthEarnings: String {
            localized("dash_last_month_earnings")
        }

        static var lastMonthJobs: String {
            localized("dash_last_month_jobs")
        }

        static var noRatingYet: String {
            localized("dash_no_rating_yet")
        }

        static func ratingCount(_ count: Int) -> String {
            format("dash_rating_count", count)
        }

        static var noJobsYetTitle: String {
            localized("dash_no_jobs_yet_title")
        }

        static var noJobsYetSubtitle: String {
            localized("dash_no_jobs_yet_subtitle")
        }

        static var availableWorkLabel: String {
            localized("dash_available_work_label")
        }

        static func availableNowCount(_ count: Int) -> String {
            format("dash_available_now_count", count)
        }

        static func earnUpTo(_ amount: String) -> String {
            format("dash_earn_up_to", amount)
        }
    }

    enum RegistrationLock {
        static var title: String {
            localized("registration_lock_title")
        }

        static var subtitle: String {
            localized("registration_lock_subtitle")
        }

        static func progress(_ completed: Int, _ total: Int) -> String {
            format("registration_lock_progress", completed, total)
        }

        static var stepComplete: String {
            localized("registration_lock_step_complete")
        }

        static var signOut: String {
            localized("registration_lock_sign_out")
        }

        static var categoryProfile: String {
            localized("registration_lock_category_profile")
        }

        static var categoryDocuments: String {
            localized("registration_lock_category_documents")
        }

        static var categoryApproval: String {
            localized("registration_lock_category_approval")
        }

        static var actionCompleteProfile: String {
            localized("registration_lock_action_complete_profile")
        }

        static var actionUploadDocuments: String {
            localized("registration_lock_action_upload_documents")
        }

        static var documentsRequired: String {
            localized("registration_lock_documents_required")
        }

        static var approvalAwaitingReview: String {
            localized("registration_lock_approval_awaiting_review")
        }

        static var approvalCompleteProfileFirst: String {
            localized("registration_lock_approval_complete_profile_first")
        }

        static var approvalRejected: String {
            localized("registration_lock_approval_rejected")
        }

        static var retry: String {
            localized("registration_lock_retry")
        }

        static func missingField(_ token: String) -> String {
            localized(token.replacingOccurrences(of: ".", with: "_"))
        }
    }

    enum Register {
        static var title: String {
            localized("create_account")
        }

        static var subtitle: String {
            localized("register_subtitle")
        }

        static var firstName: String {
            localized("first_name")
        }

        static var lastName: String {
            localized("last_name")
        }

        static var confirmPassword: String {
            localized("confirm_password")
        }

        static var acceptTerms: String {
            localized("accept_terms")
        }

        static var submit: String {
            localized("register")
        }

        static var alreadyHaveAccount: String {
            localized("already_have_account")
        }

        static var signInHere: String {
            localized("sign_in_here")
        }

        static var ruleMinLength: String {
            localized("register_pw_min_length")
        }

        static var ruleLetter: String {
            localized("register_pw_letter")
        }

        static var ruleNumber: String {
            localized("register_pw_number")
        }

        static var ruleMatch: String {
            localized("register_pw_match")
        }

        static var errorFirstNameRequired: String {
            localized("register_error_first_name_required")
        }

        static var errorLastNameRequired: String {
            localized("register_error_last_name_required")
        }

        static var errorEmailRequired: String {
            localized("register_error_email_required")
        }

        static var errorEmailInvalid: String {
            localized("register_error_email_invalid")
        }

        static var errorPasswordRules: String {
            localized("register_error_password_rules")
        }

        static var errorPasswordsNoMatch: String {
            localized("register_error_passwords_no_match")
        }

        static var errorTermsRequired: String {
            localized("register_error_terms_required")
        }
    }

    enum ConfirmEmail {
        static var title: String {
            localized("verify_email")
        }

        static var subtitle: String {
            localized("verify_email_subtitle")
        }

        static var verify: String {
            localized("verify")
        }

        static var resendCode: String {
            localized("resend_code")
        }

        static var resendSent: String {
            localized("confirm_email_subtitle")
        }

        static var errorGeneric: String {
            localized("error_generic")
        }

        static var back: String {
            localized("back")
        }
    }

    enum Shell {
        static var dashboard: String {
            localized("dashboard")
        }

        static var orders: String {
            localized("orders")
        }

        static var invoices: String {
            localized("invoices")
        }

        static var profile: String {
            localized("profile")
        }

        static func placeholderComingSoon(_ name: String, _ ticket: String) -> String {
            format("shell_tab_placeholder", name, ticket)
        }
    }

    private static func localized(_ key: String) -> String {
        String(localized: String.LocalizationValue(key))
    }

    private static func format(_ key: String, _ args: CVarArg...) -> String {
        String(format: localized(key), arguments: args)
    }
}
