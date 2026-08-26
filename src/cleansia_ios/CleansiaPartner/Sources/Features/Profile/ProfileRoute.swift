import Foundation

enum ProfileRoute: Hashable {
    case personal(onboarding: Bool)
    case address(onboarding: Bool)
    case identification(onboarding: Bool)
    case bank(onboarding: Bool)
    case emergency
    case documents
    case jobRadius
    case language
    case theme
    case devices
    /// Request account deletion. Files a request; an admin fulfils it. -> /decisions/adr-0052
    case deleteAccount
}

enum ProfileSection: Int, CaseIterable {
    case personal = 0
    case address = 1
    case identification = 2
    case bank = 3

    /// The step before this one, or nil for the first. Int-backed rather than an index-of lookup so
    /// this needs no force unwrap — `force_unwrapping` is a SwiftLint ERROR in this repo.
    var previous: ProfileSection? {
        ProfileSection(rawValue: rawValue - 1)
    }

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
