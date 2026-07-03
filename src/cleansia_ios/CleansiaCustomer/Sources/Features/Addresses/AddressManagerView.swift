import CleansiaCore
import SwiftUI

struct AddressManagerView: View {
    @StateObject private var vm: AddressManagerViewModel
    private let onBack: () -> Void
    private let onSelected: (SavedAddress) -> Void

    init(
        repository: SavedAddressRepository,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        snackbar: SnackbarController,
        onBack: @escaping () -> Void,
        onSelected: @escaping (SavedAddress) -> Void
    ) {
        _vm = StateObject(wrappedValue: AddressManagerViewModel(
            repository: repository,
            geocoding: geocoding,
            mapProvider: mapProvider,
            snackbar: snackbar
        ))
        self.onBack = onBack
        self.onSelected = onSelected
    }

    var body: some View {
        content
            .navigationBarHidden(true)
            .task { await vm.onAppear() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.pane {
        case .list:
            AddressListPane(
                addresses: vm.addresses,
                selectedId: vm.selectedId,
                onBack: onBack,
                onAdd: vm.startAdd,
                onSelect: { address in
                    vm.select(address)
                    onSelected(address)
                },
                onSetDefault: { id in Task { await vm.setDefault(id: id) } },
                onRename: { id, label in Task { await vm.rename(id: id, newLabel: label) } },
                onDelete: { id in Task { await vm.delete(id: id) } }
            )
        case .addOnMap:
            BookingAddressPickerView(
                geocoding: vm.geocoding,
                mapProvider: vm.mapProvider,
                onConfirmed: vm.mapDidConfirm,
                onBack: vm.backToList
            )
        case .reviewNew:
            if let picked = vm.pickedAddress {
                AddressReviewPane(
                    picked: picked,
                    onBack: vm.backToMap,
                    onConfirm: { label, setAsDefault in
                        Task { await vm.saveReviewed(label: label, setAsDefault: setAsDefault) }
                    }
                )
            } else {
                Color.clear.onAppear(perform: vm.backToMap)
            }
        }
    }
}

private struct AddressManagerHeader: View {
    let title: String
    let onBack: () -> Void

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Button(action: onBack) {
                Image(systemName: "chevron.left")
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(CleansiaColors.onBackground)
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel(L10n.AddressManager.back)
            Text(title)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Spacer()
        }
        .padding(.horizontal, Spacing.xs)
    }
}

private struct AddressListPane: View {
    let addresses: [SavedAddress]
    let selectedId: String?
    let onBack: () -> Void
    let onAdd: () -> Void
    let onSelect: (SavedAddress) -> Void
    let onSetDefault: (String) -> Void
    let onRename: (String, String) -> Void
    let onDelete: (String) -> Void

    @State private var renaming: SavedAddress?
    @State private var deleting: SavedAddress?

    var body: some View {
        VStack(spacing: 0) {
            AddressManagerHeader(title: L10n.AddressManager.title, onBack: onBack)
            ScrollView {
                VStack(spacing: Spacing.s) {
                    if addresses.isEmpty {
                        emptyState
                    } else {
                        ForEach(addresses) { address in
                            SavedAddressRow(
                                address: address,
                                isSelected: address.id == selectedId,
                                onSelect: { onSelect(address) },
                                onSetDefault: { onSetDefault(address.id) },
                                onRename: { renaming = address },
                                onDelete: { deleting = address }
                            )
                        }
                    }
                    addButton
                }
                .padding(.horizontal, Spacing.l)
                .padding(.top, Spacing.s)
                .padding(.bottom, Spacing.xxl)
            }
        }
        .background(CleansiaColors.background.ignoresSafeArea())
        .alert(L10n.AddressManager.renameTitle, isPresented: renameBinding, presenting: renaming) { address in
            RenameAlertButtons(initialLabel: address.label) { newLabel in
                onRename(address.id, newLabel)
            }
        }
        .confirmationDialog(
            L10n.AddressManager.deleteTitle,
            isPresented: deleteBinding,
            presenting: deleting
        ) { address in
            Button(L10n.AddressManager.delete, role: .destructive) { onDelete(address.id) }
            Button(L10n.AddressManager.cancel, role: .cancel) {}
        } message: { address in
            Text(L10n.AddressManager.deleteBody(address.oneLine))
        }
    }

    private var renameBinding: Binding<Bool> {
        Binding(get: { renaming != nil }, set: { if !$0 { renaming = nil } })
    }

    private var deleteBinding: Binding<Bool> {
        Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } })
    }

    private var emptyState: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "mappin.slash")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.AddressManager.empty)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.xxl)
    }

    private var addButton: some View {
        Button(action: onAdd) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "plus")
                Text(L10n.AddressManager.add)
                    .font(CleansiaTypography.bodyLarge)
                Spacer()
            }
            .foregroundColor(CleansiaColors.primary)
            .padding(Spacing.m)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.primary, lineWidth: 1)
            )
        }
    }
}

private struct RenameAlertButtons: View {
    let initialLabel: String
    let onConfirm: (String) -> Void
    @State private var text: String

    init(initialLabel: String, onConfirm: @escaping (String) -> Void) {
        self.initialLabel = initialLabel
        self.onConfirm = onConfirm
        _text = State(initialValue: initialLabel)
    }

    var body: some View {
        TextField(L10n.AddressManager.labelHint, text: $text)
        Button(L10n.AddressManager.save) {
            let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { onConfirm(trimmed) }
        }
        Button(L10n.AddressManager.cancel, role: .cancel) {}
    }
}

private struct SavedAddressRow: View {
    let address: SavedAddress
    let isSelected: Bool
    let onSelect: () -> Void
    let onSetDefault: () -> Void
    let onRename: () -> Void
    let onDelete: () -> Void

    var body: some View {
        HStack(spacing: Spacing.s) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.primaryContainer)
                    .frame(width: 40, height: 40)
                Image(systemName: "mappin.and.ellipse")
                    .foregroundColor(CleansiaColors.primary)
            }
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: Spacing.xs) {
                    Text(address.label)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if address.isDefault {
                        Text(L10n.AddressManager.defaultBadge)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.primary)
                            .padding(.horizontal, Spacing.xs)
                            .padding(.vertical, 2)
                            .background(
                                RoundedRectangle(cornerRadius: CornerRadius.small)
                                    .fill(CleansiaColors.primaryContainer)
                            )
                    }
                }
                Text(address.oneLine)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .lineLimit(1)
            }
            Spacer()
            Menu {
                if !address.isDefault {
                    Button(action: onSetDefault) {
                        Label(L10n.AddressManager.setDefault, systemImage: "star")
                    }
                }
                Button(action: onRename) {
                    Label(L10n.AddressManager.rename, systemImage: "pencil")
                }
                Button(role: .destructive, action: onDelete) {
                    Label(L10n.AddressManager.delete, systemImage: "trash")
                }
            } label: {
                Image(systemName: "ellipsis")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel(L10n.AddressManager.options)
        }
        .padding(Spacing.m)
        .background(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .fill(isSelected ? CleansiaColors.primaryContainer.opacity(0.3) : CleansiaColors.surface)
        )
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(
                    isSelected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                    lineWidth: isSelected ? 2 : 1
                )
        )
        .contentShape(Rectangle())
        .onTapGesture(perform: onSelect)
    }
}

private struct AddressReviewPane: View {
    let picked: GeocodedAddress
    let onBack: () -> Void
    let onConfirm: (String, Bool) -> Void

    @State private var label: String
    @State private var save = true
    @State private var setAsDefault = false

    init(picked: GeocodedAddress, onBack: @escaping () -> Void, onConfirm: @escaping (String, Bool) -> Void) {
        self.picked = picked
        self.onBack = onBack
        self.onConfirm = onConfirm
        _label = State(initialValue: picked.city.isEmpty ? L10n.AddressManager.fallbackLabel : picked.city)
    }

    var body: some View {
        VStack(spacing: 0) {
            AddressManagerHeader(title: L10n.AddressManager.reviewTitle, onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    addressCard
                    labelField
                    toggles
                }
                .padding(.horizontal, Spacing.l)
                .padding(.top, Spacing.s)
                .padding(.bottom, Spacing.xxl)
            }
            CleansiaPrimaryButton(L10n.AddressManager.confirm) {
                onConfirm(label, save ? setAsDefault : false)
            }
            .padding(.horizontal, Spacing.l)
            .padding(.bottom, Spacing.m)
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }

    private var addressCard: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(picked.street.isEmpty ? picked.formatted : picked.street)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            if let subtitle {
                Text(subtitle)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .fill(CleansiaColors.surface)
        )
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }

    private var labelField: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.AddressManager.labelHint)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaTextField(value: $label, label: L10n.AddressManager.labelHint)
        }
    }

    private var toggles: some View {
        VStack(spacing: Spacing.s) {
            Toggle(L10n.AddressManager.saveAddress, isOn: $save)
            if save {
                Toggle(L10n.AddressManager.setAsDefaultToggle, isOn: $setAsDefault)
            }
        }
        .tint(CleansiaColors.primary)
        .font(CleansiaTypography.bodyLarge)
        .foregroundColor(CleansiaColors.onSurface)
    }

    private var subtitle: String? {
        let parts = [picked.zipCode, picked.city, picked.country].filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }
}

#if DEBUG
    struct AddressManagerView_Previews: PreviewProvider {
        static var previews: some View {
            AddressManagerView(
                repository: SavedAddressRepository(client: LiveSavedAddressClient()),
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider(),
                snackbar: SnackbarController(),
                onBack: {},
                onSelected: { _ in }
            )
        }
    }
#endif
