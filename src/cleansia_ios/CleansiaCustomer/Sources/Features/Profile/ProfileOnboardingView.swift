import CleansiaCore
import SwiftUI

/// Shown once per user: it gathers what the registration form skips.
struct ProfileOnboardingView: View {
    @StateObject private var vm: ProfileViewModel
    private let onDone: () -> Void

    @State private var firstName = ""
    @State private var lastName = ""
    @State private var phone = ""
    @State private var birthDate: Date?
    @State private var seededFor: String?
    @State private var openStep: OnboardingStep?
    @State private var openedByHand = false

    init(makeViewModel: @escaping () -> ProfileViewModel, onDone: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: makeViewModel())
        self.onDone = onDone
    }

    var body: some View {
        ProfileOnboardingContent(
            greetingName: vm.currentUser.map { OnboardingStart($0).firstName } ?? "",
            email: vm.currentUser?.email ?? "",
            emailHidden: vm.currentUser?.isEmailHiddenByApple ?? false,
            checklist: vm.onboardingChecklist(firstName: firstName, lastName: lastName, phoneNumber: phone),
            openStep: $openStep,
            openedByHand: $openedByHand,
            firstName: $firstName,
            lastName: $lastName,
            phone: $phone,
            birthDate: $birthDate,
            saving: vm.saveState.isSubmitting,
            canSave: vm.canCompleteOnboarding(firstName: firstName, lastName: lastName, phoneNumber: phone),
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

    /// The open step is picked once, here; typing never moves it.
    private func seed(_ user: CurrentUserProfile?) {
        guard let user, seededFor != user.id else { return }
        seededFor = user.id
        let start = OnboardingStart(user)
        firstName = start.firstName
        lastName = start.lastName
        openStep = start.step
        phone = user.phoneNumber ?? ""
        birthDate = user.birthDate
    }
}

private struct ProfileOnboardingContent: View {
    let greetingName: String
    let email: String
    let emailHidden: Bool
    let checklist: OnboardingChecklist
    @Binding var openStep: OnboardingStep?
    @Binding var openedByHand: Bool
    @Binding var firstName: String
    @Binding var lastName: String
    @Binding var phone: String
    @Binding var birthDate: Date?
    let saving: Bool
    let canSave: Bool
    let onSkip: () -> Void
    let onSave: () -> Void

    @State private var showsBirthSheet = false
    @State private var keyboardShown = false

    private static let cardEnd = "onboarding-card-end"
    private static let keyboardWillShow = NotificationCenter.default
        .publisher(for: UIResponder.keyboardWillShowNotification)
    private static let keyboardDidShow = NotificationCenter.default
        .publisher(for: UIResponder.keyboardDidShowNotification)
    private static let keyboardWillHide = NotificationCenter.default
        .publisher(for: UIResponder.keyboardWillHideNotification)

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            VStack(spacing: 0) {
                ScrollViewReader { proxy in
                    ScrollView { content }
                        .onReceive(Self.keyboardWillShow) { _ in keyboardShown = true }
                        // Not on willShow: its observers run in no set order, so the scroll view may not have
                        // shrunk for the keyboard yet.
                        .onReceive(Self.keyboardDidShow) { _ in
                            guard openStep == .phone else { return }
                            withAnimation { proxy.scrollTo(Self.cardEnd, anchor: .bottom) }
                        }
                        .onReceive(Self.keyboardWillHide) { _ in keyboardShown = false }
                }
                footer
            }
        }
        .sheet(isPresented: $showsBirthSheet) {
            BirthDateWheelSheet(
                birthDate: $birthDate,
                title: L10n.Onboarding.birthDateLabel,
                note: L10n.Onboarding.birthDateHelper
            )
        }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 0) {
            greeting
                .padding(.top, Spacing.xxs)
            // Drawn only once seeded, so a hidden-name account never shows the phone step for a frame.
            if let step = Binding($openStep) {
                OnboardingReadinessCard(
                    checklist: checklist,
                    email: email,
                    emailHidden: emailHidden,
                    birthDate: birthDate,
                    openStep: step,
                    openedByHand: $openedByHand,
                    firstName: $firstName,
                    lastName: $lastName,
                    phone: $phone,
                    onBirthDate: { showsBirthSheet = true }
                )
                .padding(.top, Spacing.s)
            }
            Color.clear.frame(height: 0).id(Self.cardEnd)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.bottom, Spacing.xs)
    }

    private var greeting: some View {
        HStack(alignment: .center, spacing: Spacing.s) {
            Text(greetingName.isBlank ? L10n.Onboarding.greeting : L10n.Onboarding.greetingNamed(greetingName))
                .cleansiaFont(CleansiaTypography.headlineLarge)
                .foregroundColor(CleansiaColors.onBackground)
                .frame(maxWidth: .infinity, alignment: .leading)
            (checklist.isReady ? Mascot.thumbsUp : Mascot.waving).image
                .resizable()
                .scaledToFit()
                .frame(width: 76, height: 76)
        }
    }

    private var footer: some View {
        VStack(spacing: Spacing.xxs) {
            CleansiaPrimaryButton(
                L10n.Onboarding.save,
                loading: saving,
                enabled: canSave,
                action: onSave
            )
            if !keyboardShown {
                Button(action: onSkip) {
                    Text(L10n.Onboarding.skip)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .frame(maxWidth: .infinity, minHeight: 44)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .disabled(saving)
            }
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.background)
        .overlay(alignment: .top) {
            if keyboardShown {
                Rectangle().fill(CleansiaColors.outline).frame(height: 1)
            }
        }
    }
}

#if DEBUG
    private struct ProfileOnboardingPreviewHost: View {
        let greetingName: String
        let email: String
        let emailHidden: Bool

        @State private var openStep: OnboardingStep?
        @State private var openedByHand = false
        @State private var firstName: String
        @State private var lastName: String
        @State private var phone: String
        @State private var birthDate: Date?

        init(greetingName: String, email: String, emailHidden: Bool, step: OnboardingStep, phone: String) {
            self.greetingName = greetingName
            self.email = email
            self.emailHidden = emailHidden
            _openStep = State(initialValue: step)
            _firstName = State(initialValue: greetingName)
            _lastName = State(initialValue: greetingName.isEmpty ? "" : "Dvořáková")
            _phone = State(initialValue: phone)
        }

        var body: some View {
            let checklist = OnboardingChecklist(firstName: firstName, lastName: lastName, email: email, phone: phone)
            ProfileOnboardingContent(
                greetingName: greetingName,
                email: email,
                emailHidden: emailHidden,
                checklist: checklist,
                openStep: $openStep,
                openedByHand: $openedByHand,
                firstName: $firstName,
                lastName: $lastName,
                phone: $phone,
                birthDate: $birthDate,
                saving: false,
                canSave: checklist.isReady,
                onSkip: {},
                onSave: {}
            )
        }
    }

    struct ProfileOnboardingContent_Previews: PreviewProvider {
        static var previews: some View {
            ProfileOnboardingPreviewHost(
                greetingName: "Jana",
                email: "jana.dvorakova@gmail.com",
                emailHidden: false,
                step: .phone,
                phone: ""
            )
            .previewDisplayName("Empty")
            ProfileOnboardingPreviewHost(
                greetingName: "Jana",
                email: "jana.dvorakova@gmail.com",
                emailHidden: false,
                step: .phone,
                phone: "+420777123456"
            )
            .previewDisplayName("Filled")
            ProfileOnboardingPreviewHost(
                greetingName: "",
                email: "x7k2p9q8r4@privaterelay.appleid.com",
                emailHidden: true,
                step: .name,
                phone: ""
            )
            .previewDisplayName("Hidden Apple account")
        }
    }
#endif
