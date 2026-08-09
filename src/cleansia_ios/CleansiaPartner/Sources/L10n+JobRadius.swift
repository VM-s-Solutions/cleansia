import Foundation

extension L10n {
    enum JobRadius {
        static var title: String {
            localized("job_radius_title")
        }

        static var summaryAllJobs: String {
            localized("job_radius_summary_all_jobs")
        }

        static func summaryWithin(_ kilometres: Int) -> String {
            format("job_radius_summary_within", kilometres)
        }

        static var explainer: String {
            localized("job_radius_explainer")
        }

        static var limitLabel: String {
            localized("job_radius_limit_label")
        }

        static var limitOffHint: String {
            localized("job_radius_limit_off_hint")
        }

        static func value(_ kilometres: Int) -> String {
            format("job_radius_value", kilometres)
        }

        static var promptTitle: String {
            localized("job_radius_prompt_title")
        }

        static var promptBody: String {
            localized("job_radius_prompt_body")
        }

        static var promptChoose: String {
            localized("job_radius_prompt_choose")
        }

        static var promptKeepAll: String {
            localized("job_radius_prompt_keep_all")
        }
    }
}

extension JobRadiusSelection {
    var summary: String {
        switch self {
        case .anywhere: L10n.JobRadius.summaryAllJobs
        case let .within(kilometres): L10n.JobRadius.summaryWithin(kilometres)
        }
    }
}
