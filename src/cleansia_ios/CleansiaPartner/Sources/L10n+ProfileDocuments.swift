import Foundation

/// The overflow extension for `L10n.Profile` — document keys first, and since then anything else
/// that would push the enum over the cap.
///
/// Split out of `L10n+Profile.swift`. `enum L10n.Profile` had reached 435 body lines against
/// SwiftLint's 400-line `type_body_length` cap, and `--strict` makes that warning fatal — an
/// EXTENSION body is not measured by that rule, which is why `L10n+OrderActions.swift` and the
/// customer app's `L10n+ProfileStats.swift` already have this shape.
///
/// **New Profile keys belong in here, not back in `enum Profile`.** With this block moved out the
/// enum measures 393 against the 400 cap — room for two more keys, and then it is red again. Three
/// measured lines per key is the arithmetic; the blank line between them is not counted.
///
/// `L10n.localized` / `L10n.format` are qualified here and bare in `L10n+Profile.swift` — the bare
/// form resolves there only because `enum Profile` is lexically nested inside `extension L10n { }`,
/// which this file is not.
extension L10n.Profile {
    /// The onboarding chain's forward CTA. Deliberately not `saveAndContinue`: once a Back button
    /// sits beside it the row is half-width, and "Сохранить и продолжить" is 22 characters against
    /// "Далее"'s 5. The key already existed in all five locales — the pre-auth carousel uses it.
    static var next: String {
        L10n.localized("onboarding_next")
    }

    static var back: String {
        L10n.localized("back")
    }

    /// Spoken by VoiceOver on a reachable step dot. The visible label names the SECTION; without a
    /// hint nothing says the dot is a control or what tapping it does.
    static var onboardingStepJumpHint: String {
        L10n.localized("onboarding_step_jump_hint")
    }

    /// What the cleaner's country asks for, whether or not any of it is uploaded. The screen
    /// used to open on an empty box that named nothing, so the first step of onboarding was
    /// contacting support to ask which papers we wanted.
    static var documentRequirementsTitle: String {
        L10n.localized("document_requirements_title")
    }

    static var documentRequirementsSubtitle: String {
        L10n.localized("document_requirements_subtitle")
    }

    static var documentRequirementRequired: String {
        L10n.localized("document_requirement_required")
    }

    static var documentRequirementOptional: String {
        L10n.localized("document_requirement_optional")
    }

    static var documentRequirementMissing: String {
        L10n.localized("document_requirement_missing")
    }

    /// The door that needs no admin: replacing never empties the slot, so the registration
    /// lock never re-engages.
    static var documentReplace: String {
        L10n.localized("document_replace")
    }

    static var documentReplaceTitle: String {
        L10n.localized("document_replace_title")
    }

    static func documentReplaceMessage(_ fileName: String) -> String {
        L10n.format("document_replace_message", fileName)
    }

    /// The other door: nothing should be there at all, and that one an employer has to agree
    /// with. The request changes nothing until an admin answers it.
    static var documentRequestDeletion: String {
        L10n.localized("document_request_deletion")
    }

    static var documentRequestDeletionTitle: String {
        L10n.localized("document_request_deletion_title")
    }

    static var documentRequestDeletionMessage: String {
        L10n.localized("document_request_deletion_message")
    }

    static var documentDeletionReason: String {
        L10n.localized("document_deletion_reason")
    }

    static var documentDeletionRequested: String {
        L10n.localized("document_deletion_requested")
    }
}
