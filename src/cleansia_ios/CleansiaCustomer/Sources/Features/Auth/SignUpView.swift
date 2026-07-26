import CleansiaCore
import SwiftUI

struct SignUpView: View {
    @StateObject private var vm: CustomerAuthViewModel
    let onSignIn: () -> Void
    let onOutcome: (AuthOutcome) -> Void

    init(
        makeViewModel: @escaping () -> CustomerAuthViewModel,
        onSignIn: @escaping () -> Void,
        onOutcome: @escaping (AuthOutcome) -> Void
    ) {
        _vm = StateObject(wrappedValue: makeViewModel())
        self.onSignIn = onSignIn
        self.onOutcome = onOutcome
    }

    var body: some View {
        SignUpContent(
            form: vm.signUpForm,
            isLoading: vm.signUpState.isSubmitting,
            isSocialLoading: vm.socialState.isSubmitting,
            referralState: vm.referralState,
            onFirstNameChange: vm.onFirstNameChange,
            onLastNameChange: vm.onLastNameChange,
            onEmailChange: vm.onSignUpEmailChange,
            onPasswordChange: vm.onSignUpPasswordChange,
            onConfirmPasswordChange: vm.onConfirmPasswordChange,
            onValidateReferral: vm.validateReferralCode,
            onClearReferral: vm.clearReferralCode,
            onAcceptTermsChange: vm.onAcceptTermsChange,
            onSignIn: onSignIn,
            onSubmit: { Task { await vm.signUp() } },
            onApple: { Task { await vm.signInWithApple() } },
            onGoogle: { Task { await vm.signInWithGoogle() } }
        )
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

private struct SignUpContent: View {
    let form: SignUpFormState
    let isLoading: Bool
    let isSocialLoading: Bool
    let referralState: ReferralCodeState
    let onFirstNameChange: (String) -> Void
    let onLastNameChange: (String) -> Void
    let onEmailChange: (String) -> Void
    let onPasswordChange: (String) -> Void
    let onConfirmPasswordChange: (String) -> Void
    let onValidateReferral: (String) async -> ReferralCodeState
    let onClearReferral: () -> Void
    let onAcceptTermsChange: (Bool) -> Void
    let onSignIn: () -> Void
    let onSubmit: () -> Void
    let onApple: () -> Void
    let onGoogle: () -> Void

    @State private var showReferralSheet = false

    private func binding(_ value: String, _ setter: @escaping (String) -> Void) -> Binding<String> {
        Binding(get: { value }, set: setter)
    }

    private var acceptTermsBinding: Binding<Bool> {
        Binding(get: { form.acceptTerms }, set: onAcceptTermsChange)
    }

    private var formDisabled: Bool {
        isLoading || isSocialLoading
    }

    var body: some View {
        CenteredAuthScroll {
            VStack(spacing: 0) {
                AuthHeaderImage()

                Spacer().frame(height: Spacing.l)

                Text(L10n.Auth.signUpTitle)
                    .font(CleansiaTypography.displayMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                    .multilineTextAlignment(.center)

                Spacer().frame(height: Spacing.xs)

                Text(L10n.Auth.signUpSubtitle)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)

                Spacer().frame(height: Spacing.xl)

                HStack(spacing: Spacing.s) {
                    CleansiaTextField(
                        value: binding(form.firstName, onFirstNameChange),
                        label: L10n.Auth.firstName,
                        errorText: form.firstNameError,
                        textContentType: .givenName,
                        enabled: !formDisabled
                    )
                    CleansiaTextField(
                        value: binding(form.lastName, onLastNameChange),
                        label: L10n.Auth.lastName,
                        errorText: form.lastNameError,
                        textContentType: .familyName,
                        enabled: !formDisabled
                    )
                }

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: binding(form.email, onEmailChange),
                    label: L10n.Auth.email,
                    errorText: form.emailError,
                    keyboardType: .emailAddress,
                    textContentType: .emailAddress,
                    enabled: !formDisabled
                )

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: binding(form.password, onPasswordChange),
                    label: L10n.Auth.password,
                    errorText: form.passwordError,
                    textContentType: .newPassword,
                    isPassword: true,
                    enabled: !formDisabled
                )

                PasswordRuleList(
                    rules: [
                        PasswordRule(label: L10n.Auth.ruleMinLength, isSatisfied: form.passwordHasMinLength),
                        PasswordRule(label: L10n.Auth.ruleLetter, isSatisfied: form.passwordHasLetter),
                        PasswordRule(label: L10n.Auth.ruleNumber, isSatisfied: form.passwordHasNumber)
                    ],
                    hasInput: !form.password.isEmpty
                )

                Spacer().frame(height: Spacing.xs)

                CleansiaTextField(
                    value: binding(form.confirmPassword, onConfirmPasswordChange),
                    label: L10n.Auth.confirmPassword,
                    errorText: form.confirmPasswordError,
                    textContentType: .newPassword,
                    isPassword: true,
                    enabled: !formDisabled
                )

                PasswordRuleList(
                    rules: [
                        PasswordRule(label: L10n.Auth.ruleMatch, isSatisfied: form.passwordsMatch)
                    ],
                    hasInput: !form.confirmPassword.isEmpty
                )

                Spacer().frame(height: Spacing.xs)

                referralRow

                Spacer().frame(height: Spacing.s)

                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    CleansiaConsentCheckbox(
                        checked: acceptTermsBinding,
                        markdown: L10n.Auth.acceptTerms,
                        toggleAccessibilityLabel: L10n.Auth.acceptTermsToggle
                    )
                    if let termsError = form.termsError {
                        Text(termsError)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.error)
                            .padding(.horizontal, Spacing.xxs)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .disabled(formDisabled)

                Spacer().frame(height: Spacing.m)

                CleansiaPrimaryButton(
                    L10n.Auth.signUp,
                    loading: isLoading,
                    enabled: form.isValid && !isSocialLoading,
                    action: onSubmit
                )

                Spacer().frame(height: Spacing.m)

                SocialSignInSection(isLoading: isSocialLoading, onApple: onApple, onGoogle: onGoogle)

                Spacer().frame(height: Spacing.l)

                HStack(spacing: 0) {
                    Text(L10n.Auth.alreadyHaveAccount)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    CleansiaTextLink(L10n.Auth.signInLink, action: onSignIn)
                }
            }
            .padding(.horizontal, Spacing.l)
            .padding(.vertical, Spacing.xl)
        }
        .background(CleansiaColors.background.ignoresSafeArea())
        .overlay {
            if isSocialLoading {
                AuthAuthenticatingOverlay()
            }
        }
        .sheet(isPresented: $showReferralSheet) {
            ReferralCodeSheet(
                initialCode: form.referralCode,
                onValidate: onValidateReferral,
                onDismiss: { showReferralSheet = false }
            )
        }
    }

    /// The referral code is optional, so it is a tappable row rather than a field
    /// in the required stack — the same `CodeEntryRow` the booking Confirm step
    /// uses for the promo code, so the two code entries behave identically.
    ///
    /// This replaces a plain disclosure holding an unvalidated text field: a typo
    /// used to be accepted silently at signup and only discovered as a missing
    /// bonus. The sheet checks the code against the server before it is applied.
    private var referralRow: some View {
        CodeEntryRow(
            systemImage: "gift",
            title: L10n.Auth.referralToggle,
            appliedCode: appliedReferralCode,
            clearLabel: L10n.Booking.referralRowClear,
            appliedText: L10n.Booking.referralRowApplied,
            onTap: { showReferralSheet = true },
            onClear: onClearReferral
        )
        .disabled(formDisabled)
    }

    /// Only a server-validated code counts as applied. An unresolved or rejected
    /// one leaves the row in its neutral "add a code" state rather than claiming
    /// a bonus the customer will not get.
    private var appliedReferralCode: String {
        if case .valid = referralState { return form.referralCode }
        return ""
    }
}

#if DEBUG
    struct SignUpView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                preview(form: SignUpFormState(), isLoading: false)
                    .previewDisplayName("Idle")
                preview(
                    form: SignUpFormState(
                        firstName: "Jana",
                        lastName: "Nováková",
                        email: "jana@b.cz",
                        password: "abcdefg1",
                        confirmPassword: "abcdefg1",
                        acceptTerms: true
                    ),
                    isLoading: true
                )
                .previewDisplayName("Submitting")
                preview(
                    form: SignUpFormState(
                        firstName: "",
                        email: "bad",
                        password: "short",
                        firstNameError: "First name is required",
                        emailError: "Please enter a valid email",
                        passwordError: "Password must be at least 8 characters with a letter and a number",
                        termsError: "You must accept the terms and conditions"
                    ),
                    isLoading: false
                )
                .previewDisplayName("Field errors")
                preview(
                    form: SignUpFormState(referralCode: "ANNA7"),
                    isLoading: false,
                    referralState: .valid(referrerFirstName: "Eva")
                )
                .previewDisplayName("Referral applied")
            }
        }

        private static func preview(
            form: SignUpFormState,
            isLoading: Bool,
            referralState: ReferralCodeState = .idle
        ) -> some View {
            SignUpContent(
                form: form,
                isLoading: isLoading,
                isSocialLoading: false,
                referralState: referralState,
                onFirstNameChange: { _ in },
                onLastNameChange: { _ in },
                onEmailChange: { _ in },
                onPasswordChange: { _ in },
                onConfirmPasswordChange: { _ in },
                onValidateReferral: { _ in .idle },
                onClearReferral: {},
                onAcceptTermsChange: { _ in },
                onSignIn: {},
                onSubmit: {},
                onApple: {},
                onGoogle: {}
            )
        }
    }
#endif
