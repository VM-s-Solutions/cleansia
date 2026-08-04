import CleansiaCore
import SwiftUI

struct EmergencySectionView: View {
    @StateObject private var vm: EmergencySectionViewModel
    private let onSaved: () -> Void

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        onSaved: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: EmergencySectionViewModel(client: client, snackbar: snackbar))
        self.onSaved = onSaved
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.emergencyContact,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            form: {
                CleansiaTextField(
                    value: $vm.name,
                    label: L10n.Profile.emergencyName,
                    errorText: vm.nameError
                )
                CleansiaPhoneInput(
                    value: $vm.phone,
                    label: L10n.Profile.emergencyPhone,
                    errorText: vm.phoneError
                )
                SaveSectionButton(
                    onboarding: false,
                    isSubmitting: vm.action.isSubmitting,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
    }
}
