import CleansiaCore
import SwiftUI

/// Post-signin onboarding (`ProfileOnboardingScreen.kt` parity). Shown once per
/// user — gathers the fields the registration form intentionally skips. Phone
/// is the practically-required one (the booking pre-flight rejects an empty
/// phone); birth date is optional. Name fields only appear for the accounts that
/// arrived without one (Apple sign-in) — everyone else sees the screen unchanged.
struct ProfileOnboardingView: View {
    @StateObject private var vm: ProfileViewModel
    private let onDone: () -> Void

    @State private var firstName = ""
    @State private var lastName = ""
    @State private var phone = ""
    @State private var birthDate: Date?
    @State private var seededFor: String?

    init(makeViewModel: @escaping () -> ProfileViewModel, onDone: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: makeViewModel())
        self.onDone = onDone
    }

    var body: some View {
        ProfileOnboardingContent(
            greetingName: vm.currentUser?.firstName ?? "",
            showNameFields: vm.onboardingNeedsName,
            firstName: $firstName,
            lastName: $lastName,
            phone: $phone,
            birthDate: $birthDate,
            saving: vm.saveState.isSubmitting,
            canSave: vm.canCompleteOnboarding(
                firstName: firstName,
                lastName: lastName,
                phoneNumber: phone
            ),
            onSkip: {
                vm.skipOnboarding()
                onDone()
            },
            onSave: {
                Task {
                    await vm.completeOnboarding(
                        firstName: firstName,
                        lastName: lastName,
                        phoneNumber: phone,
                        birthDate: birthDate
                    )
                }
            }
        )
        .onReceive(vm.repository.$currentUser) { user in seed(user) }
        .onReceive(vm.saved) { onDone() }
    }

    private func seed(_ user: CurrentUserProfile?) {
        guard let user, seededFor != user.id else { return }
        seededFor = user.id
        firstName = user.firstName
        lastName = user.lastName
        phone = user.phoneNumber ?? ""
        birthDate = user.birthDate
    }
}

private struct ProfileOnboardingContent: View {
    let greetingName: String
    let showNameFields: Bool
    @Binding var firstName: String
    @Binding var lastName: String
    @Binding var phone: String
    @Binding var birthDate: Date?
    let saving: Bool
    let canSave: Bool
    let onSkip: () -> Void
    let onSave: () -> Void

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            VStack(spacing: 0) {
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        hero
                            .padding(.top, Spacing.l)
                        if showNameFields {
                            CleansiaTextField(
                                value: $firstName,
                                label: L10n.Onboarding.firstNameLabel,
                                textContentType: .givenName
                            )
                            .padding(.top, 28)
                            CleansiaTextField(
                                value: $lastName,
                                label: L10n.Onboarding.lastNameLabel,
                                textContentType: .familyName
                            )
                            .padding(.top, Spacing.m)
                        }
                        CleansiaPhoneInput(
                            value: $phone,
                            label: L10n.Onboarding.phoneLabel,
                            helper: L10n.Onboarding.phoneHelper
                        )
                        .padding(.top, showNameFields ? Spacing.m : 28)
                        OnboardingDateField(birthDate: $birthDate)
                            .padding(.top, Spacing.m)
                    }
                    .padding(.horizontal, Spacing.ml)
                    .padding(.bottom, Spacing.xl)
                }
                footer
            }
        }
    }

    private var hero: some View {
        VStack(spacing: 0) {
            Mascot.waving.image
                .resizable()
                .scaledToFit()
                .frame(width: 160, height: 160)
            Text(
                greetingName.isBlank
                    ? L10n.Onboarding.greeting
                    : L10n.Onboarding.greetingNamed(greetingName)
            )
            .cleansiaFont(CleansiaTypography.headlineSmall)
            .foregroundColor(CleansiaColors.onBackground)
            .padding(.top, Spacing.m)
            Text(L10n.Onboarding.subtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
                .padding(.top, Spacing.xs)
                .padding(.horizontal, Spacing.xs)
        }
        .frame(maxWidth: .infinity)
    }

    private var footer: some View {
        VStack(spacing: Spacing.xs) {
            CleansiaPrimaryButton(
                L10n.Onboarding.save,
                loading: saving,
                enabled: canSave,
                action: onSave
            )
            Button(action: onSkip) {
                Text(L10n.Onboarding.skip)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .disabled(saving)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.background)
    }
}

private struct OnboardingDateField: View {
    @Binding var birthDate: Date?
    @State private var showPicker = false

    private static let formatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        return formatter
    }()

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Onboarding.birthDateLabel)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Button {
                showPicker = true
            } label: {
                HStack {
                    Image(systemName: "calendar")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(
                        birthDate.map { Self.formatter.string(from: $0) }
                            ?? L10n.Onboarding.birthDatePlaceholder
                    )
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(birthDate == nil ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurface)
                    Spacer()
                }
                .padding(Spacing.m)
                .background(CleansiaColors.surface)
                .overlay(
                    RoundedRectangle(cornerRadius: CornerRadius.small)
                        .stroke(CleansiaColors.outline, lineWidth: 1)
                )
                .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            }
            .buttonStyle(.plain)
            Text(L10n.Onboarding.birthDateHelper)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.horizontal, Spacing.xxs)
        }
        .sheet(isPresented: $showPicker) {
            DatePicker(
                L10n.Onboarding.birthDateLabel,
                selection: Binding(get: { birthDate ?? Date() }, set: { birthDate = $0 }),
                in: ...Date(),
                displayedComponents: .date
            )
            .datePickerStyle(.graphical)
            .padding()
            .presentationDetents([.medium])
        }
    }
}

#if DEBUG
    private struct ProfileOnboardingPreviewHost: View {
        let greetingName: String
        let showNameFields: Bool

        @State private var firstName = ""
        @State private var lastName = ""
        @State private var phone = ""
        @State private var birthDate: Date?

        var body: some View {
            ProfileOnboardingContent(
                greetingName: greetingName,
                showNameFields: showNameFields,
                firstName: $firstName,
                lastName: $lastName,
                phone: $phone,
                birthDate: $birthDate,
                saving: false,
                canSave: false,
                onSkip: {},
                onSave: {}
            )
        }
    }

    struct ProfileOnboardingContent_Previews: PreviewProvider {
        static var previews: some View {
            ProfileOnboardingPreviewHost(greetingName: "Jane", showNameFields: false)
                .previewDisplayName("Named")
            ProfileOnboardingPreviewHost(greetingName: "", showNameFields: true)
                .previewDisplayName("Nameless account")
        }
    }
#endif
