import CleansiaCore
import SwiftUI

struct JobRadiusSectionView: View {
    @StateObject private var vm: JobRadiusSectionViewModel
    private let onSaved: (Int?) -> Void

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        onSaved: @escaping (Int?) -> Void
    ) {
        _vm = StateObject(wrappedValue: JobRadiusSectionViewModel(client: client, snackbar: snackbar))
        self.onSaved = onSaved
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    var body: some View {
        SectionScaffold(
            title: L10n.JobRadius.title,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            form: {
                JobRadiusControl(
                    form: vm.form,
                    interactive: !vm.action.isSubmitting,
                    onLimitedChange: vm.setLimited,
                    onKilometresChange: vm.setKilometres
                )
                SaveSectionButton(
                    onboarding: false,
                    isSubmitting: vm.action.isSubmitting,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved($0) }
    }
}
