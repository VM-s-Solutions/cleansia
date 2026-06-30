import CleansiaCore
import SwiftUI

struct CreateDisputeView: View {
    @StateObject private var vm: CreateDisputeViewModel
    @State private var reasonId: String?
    @State private var description = ""

    let onCreated: (String) -> Void

    init(
        orderId: String?,
        repository: DisputeRepository,
        snackbar: SnackbarController,
        onCreated: @escaping (String) -> Void
    ) {
        _vm = StateObject(wrappedValue: CreateDisputeViewModel(
            orderId: orderId,
            repository: repository,
            snackbar: snackbar
        ))
        self.onCreated = onCreated
    }

    private var reasonValue: Int? {
        reasonId.flatMap(Int.init)
    }

    private var canSubmit: Bool {
        reasonValue != nil &&
            vm.descriptionIsValid(description.trimmingCharacters(in: .whitespacesAndNewlines)) &&
            vm.hasOrderContext &&
            !vm.submitState.isSubmitting
    }

    var body: some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.m) {
                    contextBanner
                    reasonField
                    descriptionField
                    if let error = vm.submitState.errorMessage {
                        Text(error)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.error)
                    }
                }
                .padding(.horizontal, Spacing.ml)
                .padding(.vertical, Spacing.m)
            }
            submitFooter
        }
        .navigationTitle(L10n.Disputes.createTitle)
        .navigationBarTitleDisplayMode(.inline)
        .background(CleansiaColors.background.ignoresSafeArea())
        .onReceive(vm.created) { id in onCreated(id) }
    }

    @ViewBuilder
    private var contextBanner: some View {
        if let orderId = vm.orderId {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(L10n.Disputes.createOrderLabel)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Text(verbatim: orderId)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(Spacing.m)
            .background(CleansiaColors.primary.opacity(0.08), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        } else {
            HStack(alignment: .top, spacing: Spacing.s) {
                Image(systemName: "exclamationmark.triangle")
                    .foregroundColor(CleansiaColors.error)
                Text(L10n.Disputes.createMissingOrder)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(Spacing.m)
            .background(
                CleansiaColors.errorContainer.opacity(0.5),
                in: RoundedRectangle(cornerRadius: CornerRadius.medium)
            )
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.error.opacity(0.35), lineWidth: 1)
            )
        }
    }

    private var reasonField: some View {
        CleansiaDropdown(
            selectedId: $reasonId,
            options: DisputeReasonOption.all.map {
                CleansiaDropdownOption(id: String($0.value), label: $0.label)
            },
            label: L10n.Disputes.createReasonLabel,
            placeholder: L10n.Disputes.createReasonLabel,
            enabled: vm.hasOrderContext && !vm.submitState.isSubmitting
        )
        .onChange(of: reasonId) { _ in vm.clearError() }
    }

    private var descriptionField: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.Disputes.createDescriptionLabel)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            ZStack(alignment: .topLeading) {
                if description.isEmpty {
                    Text(L10n.Disputes.createDescriptionPlaceholder)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .padding(.horizontal, Spacing.m)
                        .padding(.vertical, 12)
                }
                TextEditor(text: $description)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                    .scrollContentBackground(.hidden)
                    .frame(minHeight: 120)
                    .padding(.horizontal, Spacing.s)
                    .padding(.vertical, 6)
                    .disabled(!vm.hasOrderContext || vm.submitState.isSubmitting)
                    .onChange(of: description) { next in
                        if next.count > DisputeFormConstants.descriptionMaxLength {
                            description = String(next.prefix(DisputeFormConstants.descriptionMaxLength))
                        }
                        vm.clearError()
                    }
            }
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
            Text(L10n.Disputes.createCharCount(description.count))
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .frame(maxWidth: .infinity, alignment: .trailing)
        }
    }

    private var submitFooter: some View {
        VStack {
            CleansiaPrimaryButton(
                L10n.Disputes.createSubmit,
                loading: vm.submitState.isSubmitting,
                enabled: canSubmit
            ) {
                guard let reasonValue else { return }
                Task { await vm.submit(reason: reasonValue, description: description) }
            }
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
    }
}

#if DEBUG
    struct CreateDisputeView_Previews: PreviewProvider {
        static var previews: some View {
            NavigationStack {
                CreateDisputeView(
                    orderId: "order-1",
                    repository: DisputeRepository(client: LiveDisputeClient()),
                    snackbar: SnackbarController(),
                    onCreated: { _ in }
                )
            }
        }
    }
#endif
