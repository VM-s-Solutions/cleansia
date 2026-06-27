import CleansiaCore
import SwiftUI

struct BankSectionView: View {
    @StateObject private var vm: BankSectionViewModel
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
        _vm = StateObject(wrappedValue: BankSectionViewModel(client: client, snackbar: snackbar))
        self.chainVM = chainVM
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.bankDetails,
            isLoading: vm.state.isLoading,
            header: {
                if onboarding {
                    OnboardingChainHeader(currentSection: .bank, state: chainVM.state)
                }
            },
            form: {
                CleansiaTextField(
                    value: $vm.iban,
                    label: L10n.Profile.iban,
                    errorText: vm.ibanError
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
