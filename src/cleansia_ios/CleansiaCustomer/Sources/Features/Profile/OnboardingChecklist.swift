import CleansiaCore

enum OnboardingStep: Equatable {
    case name
    case phone
}

/// What the onboarding opens on, read once from the saved profile. The server's stand-in for a hidden Apple name
/// counts as no name, so it is never prefilled or greeted.
struct OnboardingStart: Equatable {
    let step: OnboardingStep
    let firstName: String
    let lastName: String

    init(_ user: CurrentUserProfile) {
        firstName = user.isNamePlaceholder ? "" : user.firstName
        lastName = user.isNamePlaceholder ? "" : user.lastName
        step = firstName.isBlank || lastName.isBlank ? .name : .phone
    }
}

/// What a booking needs, judged on what the form holds now.
struct OnboardingChecklist: Equatable {
    enum Prompt: Equatable { case addName, addPhone, addBoth, ready }

    let name: Bool
    let email: Bool
    let phone: Bool

    init(firstName: String, lastName: String, email: String, phone: String) {
        name = !firstName.isBlank && !lastName.isBlank
        self.email = !email.isBlank
        self.phone = PhoneNumberKitDisplayFormatter.isPossibleNumber(phone)
    }

    var doneCount: Int {
        [name, email, phone].filter { $0 }.count
    }

    var isReady: Bool {
        name && email && phone
    }

    /// The mapper refuses a blank e-mail, so the prompt only ever names the name and the phone.
    var prompt: Prompt {
        if isReady { return .ready }
        if !name, !phone { return .addBoth }
        return name ? .addPhone : .addName
    }
}
