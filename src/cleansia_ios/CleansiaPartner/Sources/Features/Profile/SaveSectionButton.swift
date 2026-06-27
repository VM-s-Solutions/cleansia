import CleansiaCore
import SwiftUI

struct SaveSectionButton: View {
    let onboarding: Bool
    let isSubmitting: Bool
    var enabled: Bool = true
    let action: () -> Void

    var body: some View {
        CleansiaPrimaryButton(
            onboarding ? L10n.Profile.saveAndContinue : L10n.Profile.save,
            loading: isSubmitting,
            enabled: enabled && !isSubmitting,
            action: action
        )
        .padding(.top, Spacing.s)
    }
}
