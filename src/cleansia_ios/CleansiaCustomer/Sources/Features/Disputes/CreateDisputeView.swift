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
        orderClient: OrderClient,
        snackbar: SnackbarController,
        onCreated: @escaping (String) -> Void
    ) {
        _vm = StateObject(wrappedValue: CreateDisputeViewModel(
            orderId: orderId,
            repository: repository,
            orderClient: orderClient,
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
                    itemsField
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
        .task { await vm.loadLineOptions() }
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

    /// Which parts went wrong.
    ///
    /// Optional throughout — a dispute about the whole job, or about a charge, ticks nothing, which
    /// is the common case and stays the path of least resistance. Absent entirely until the order's
    /// items load, and absent forever on the FAB flow that arrives with no order.
    ///
    /// Toggles rather than the reason chips above: that row is pick-one and looks it, and repeating
    /// the shape here would teach the wrong gesture for a multi-select.
    @ViewBuilder
    private var itemsField: some View {
        if !vm.lineOptions.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(L10n.Disputes.createWhichItems)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Text(L10n.Disputes.createWhichItemsHint)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ForEach(vm.lineOptions) { option in
                    Button {
                        vm.toggleLine(option)
                    } label: {
                        HStack(alignment: .top, spacing: Spacing.xs) {
                            Image(systemName: vm.pickedLineIds.contains(option.id)
                                ? "checkmark.square.fill"
                                : "square")
                                .foregroundColor(CleansiaColors.primary)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(option.label)
                                    .font(CleansiaTypography.bodyMedium)
                                    .foregroundColor(CleansiaColors.onSurface)
                                // The bundle it came in. Quiet: two rows can share a name and only
                                // this tells them apart.
                                if let packageLabel = option.packageLabel, !packageLabel.isEmpty {
                                    Text(L10n.Disputes.createItemInPackage(packageLabel))
                                        .font(CleansiaTypography.labelSmall)
                                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                                }
                            }
                            Spacer(minLength: 0)
                        }
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityAddTraits(
                        vm.pickedLineIds.contains(option.id) ? .isSelected : []
                    )
                }
            }
        }
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
                    orderClient: LiveOrderClient(),
                    snackbar: SnackbarController(),
                    onCreated: { _ in }
                )
            }
        }
    }
#endif
