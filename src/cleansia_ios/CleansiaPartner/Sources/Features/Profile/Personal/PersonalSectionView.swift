import CleansiaCore
import SwiftUI

struct PersonalSectionView: View {
    @StateObject private var vm: PersonalSectionViewModel
    @ObservedObject private var chainVM: OnboardingChainViewModel
    private let onboarding: Bool
    private let onSaved: () -> Void

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        chainVM: OnboardingChainViewModel,
        onboarding: Bool,
        onSaved: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: PersonalSectionViewModel(client: client, snackbar: snackbar))
        self.chainVM = chainVM
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.personal,
            isLoading: vm.state.isLoading,
            header: {
                if onboarding {
                    OnboardingChainHeader(currentSection: .personal, state: chainVM.state)
                }
            },
            form: {
                CleansiaTextField(
                    value: $vm.form.firstName,
                    label: L10n.Profile.firstName,
                    errorText: vm.form.firstNameError
                )
                CleansiaTextField(
                    value: $vm.form.lastName,
                    label: L10n.Profile.lastName,
                    errorText: vm.form.lastNameError
                )
                BirthDateField(
                    birthDate: $vm.form.birthDate,
                    label: L10n.Profile.birthDate,
                    placeholder: L10n.Profile.birthDatePlaceholder,
                    errorText: vm.form.birthDateError
                )
                CleansiaTextField(
                    value: $vm.form.phone,
                    label: L10n.Profile.phone,
                    keyboardType: .phonePad
                )
                CleansiaTextField(
                    value: $vm.form.email,
                    label: L10n.Profile.email,
                    keyboardType: .emailAddress
                )
                SaveSectionButton(
                    onboarding: onboarding,
                    isSubmitting: vm.action.isSubmitting,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
    }
}
