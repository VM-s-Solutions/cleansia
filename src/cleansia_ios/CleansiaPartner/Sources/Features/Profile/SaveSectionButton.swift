import CleansiaCore
import SwiftUI

struct SaveSectionButton: View {
    let onboarding: Bool
    let isSubmitting: Bool
    var enabled: Bool = true

    /// Non-nil only inside the onboarding chain, and only when a previous step exists. The profile
    /// menu reaches these same screens for a maintenance edit, where there is nothing to go back to
    /// and the toolbar arrow is already the way out.
    var onBack: (() -> Void)?
    let action: () -> Void

    var body: some View {
        HStack(spacing: Spacing.s) {
            if let onBack {
                CleansiaOutlinedButton(
                    L10n.Profile.back,
                    size: .large,
                    enabled: !isSubmitting,
                    action: onBack
                )
            }
            CleansiaPrimaryButton(
                // "Next", not "Save & continue": with a Back button beside it the row is half as
                // wide, and the Russian and Ukrainian strings are 22 characters against "Далее"'s 5.
                // The key already existed in all five locales — the pre-auth carousel uses it.
                onboarding ? L10n.Profile.next : L10n.Profile.save,
                loading: isSubmitting,
                enabled: enabled && !isSubmitting,
                action: action
            )
        }
        .padding(.top, Spacing.s)
    }
}
