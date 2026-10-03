import Foundation

extension L10n.Booking {
    static var dirtinessQuestion: String {
        L10n.localized("booking_dirtiness_title")
    }

    static var dirtinessHint: String {
        L10n.localized("booking_dirtiness_hint")
    }

    static var dirtinessLevelLabel: String {
        L10n.localized("booking_dirtiness_level_label")
    }

    static func dirtinessName(_ level: Dirtiness) -> String {
        switch level {
        case .normal: L10n.localized("booking_dirtiness_normal_name")
        case .increased: L10n.localized("booking_dirtiness_increased_name")
        case .heavy: L10n.localized("booking_dirtiness_heavy_name")
        }
    }

    static func dirtinessRate(_ level: Dirtiness) -> String {
        switch level {
        case .normal: L10n.localized("booking_dirtiness_normal_rate")
        case .increased: L10n.localized("booking_dirtiness_increased_rate")
        case .heavy: L10n.localized("booking_dirtiness_heavy_rate")
        }
    }

    static func dirtinessLead(_ level: Dirtiness) -> String {
        switch level {
        case .normal: L10n.localized("booking_dirtiness_normal_lead")
        case .increased: L10n.localized("booking_dirtiness_increased_lead")
        case .heavy: L10n.localized("booking_dirtiness_heavy_lead")
        }
    }

    static func dirtinessSigns(_ level: Dirtiness) -> [String] {
        switch level {
        case .normal:
            [
                L10n.localized("booking_dirtiness_normal_sign_1"),
                L10n.localized("booking_dirtiness_normal_sign_2"),
                L10n.localized("booking_dirtiness_normal_sign_3"),
                L10n.localized("booking_dirtiness_normal_sign_4")
            ]
        case .increased:
            [
                L10n.localized("booking_dirtiness_increased_sign_1"),
                L10n.localized("booking_dirtiness_increased_sign_2"),
                L10n.localized("booking_dirtiness_increased_sign_3"),
                L10n.localized("booking_dirtiness_increased_sign_4")
            ]
        case .heavy:
            [
                L10n.localized("booking_dirtiness_heavy_sign_1"),
                L10n.localized("booking_dirtiness_heavy_sign_2"),
                L10n.localized("booking_dirtiness_heavy_sign_3"),
                L10n.localized("booking_dirtiness_heavy_sign_4")
            ]
        }
    }

    /// The booking's surcharge line, stating today's rate; Normal adds none.
    static func dirtinessSurcharge(_ level: Dirtiness) -> String? {
        switch level {
        case .normal: nil
        case .increased: L10n.localized("booking_dirtiness_surcharge_increased")
        case .heavy: L10n.localized("booking_dirtiness_surcharge_heavy")
        }
    }

    /// A booked order's surcharge was charged at its booking's rate, which need not be today's, so its
    /// line names no rate.
    static func bookedDirtinessSurcharge(_ level: Dirtiness) -> String? {
        switch level {
        case .normal: nil
        case .increased: L10n.localized("booking_dirtiness_surcharge_line_increased")
        case .heavy: L10n.localized("booking_dirtiness_surcharge_line_heavy")
        }
    }
}
