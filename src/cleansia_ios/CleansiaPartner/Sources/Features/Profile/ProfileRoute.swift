import Foundation

enum ProfileRoute: Hashable {
    case personal(onboarding: Bool)
    case address(onboarding: Bool)
    case identification(onboarding: Bool)
    case bank(onboarding: Bool)
    case emergency
    case documents
    case language
    case theme
    case devices
}

enum ProfileSection: CaseIterable {
    case personal
    case address
    case identification
    case bank

    func route(onboarding: Bool) -> ProfileRoute {
        switch self {
        case .personal: .personal(onboarding: onboarding)
        case .address: .address(onboarding: onboarding)
        case .identification: .identification(onboarding: onboarding)
        case .bank: .bank(onboarding: onboarding)
        }
    }

    var ownedFields: Set<String> {
        switch self {
        case .personal:
            [
                "profile.fields.firstName",
                "profile.fields.lastName",
                "profile.fields.email",
                "profile.fields.phoneNumber",
                "profile.fields.birthDate"
            ]
        case .address:
            [
                "profile.fields.street",
                "profile.fields.city",
                "profile.fields.zipCode",
                "profile.fields.country"
            ]
        case .identification:
            [
                "profile.fields.passportId",
                "profile.fields.nationality",
                "profile.fields.registrationNumber",
                "profile.fields.legalEntityName"
            ]
        case .bank:
            ["profile.fields.iban"]
        }
    }
}

enum ProfileSectionRouting {
    static func firstMissingSection(
        missingFields: [String],
        forOnboarding: Bool
    ) -> ProfileRoute {
        let missing = Set(missingFields)
        for section in ProfileSection.allCases where !section.ownedFields.isDisjoint(with: missing) {
            return section.route(onboarding: forOnboarding)
        }
        return ProfileSection.personal.route(onboarding: forOnboarding)
    }
}
