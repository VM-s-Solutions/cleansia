import CleansiaCore
import Combine
import SwiftUI

private struct SavedAddressRepositoryKey: EnvironmentKey {
    @MainActor static var defaultValue: SavedAddressRepository?
}

extension EnvironmentValues {
    var savedAddressRepository: SavedAddressRepository? {
        get { self[SavedAddressRepositoryKey.self] }
        set { self[SavedAddressRepositoryKey.self] = newValue }
    }
}

enum BookingSavedAddressSelection {
    static func preselectedId(
        addresses: [SavedAddress],
        currentSavedAddressId: String?,
        repoSelectedId: String?
    ) -> String? {
        if let currentSavedAddressId, addresses.contains(where: { $0.id == currentSavedAddressId }) {
            return currentSavedAddressId
        }
        return (addresses.first { $0.id == repoSelectedId }
            ?? addresses.first(where: \.isDefault)
            ?? addresses.first)?.id
    }
}

enum BookingSavedAddressApply {
    /// Mirrors Android `BookingBottomSheet.kt:661-670` onAddressSelected — a
    /// saved pick keeps `savedAddressId` so the order command sends it (and no
    /// inline address), matching the Home/AddressManager selection. Marked
    /// hand-picked (`hydratedFromSavedId = nil`) so a later preferred-address
    /// re-open won't overwrite it.
    static func applied(_ state: BookingState, address: SavedAddress) -> BookingState {
        var next = state
        next.street = address.street
        next.city = address.city
        next.zipCode = address.zipCode
        next.savedAddressId = address.id
        next.hydratedFromSavedId = nil
        return next
    }
}

@MainActor
final class BookingSavedAddressChooserModel: ObservableObject {
    @Published private(set) var addresses: [SavedAddress] = []
    @Published private(set) var selectedId: String?
    @Published private(set) var loading = false

    private let repository: SavedAddressRepository?

    init(repository: SavedAddressRepository?) {
        self.repository = repository
        guard let repository else { return }
        repository.$addresses.assign(to: &$addresses)
        repository.$selectedId.assign(to: &$selectedId)
    }

    func onAppear() async {
        guard let repository, !repository.loaded, !repository.loading else { return }
        loading = true
        _ = await repository.refresh()
        loading = false
    }
}

/// Booking's saved-address chooser — the iOS parity for tapping the booking
/// address row, which on Android opens the shared Address Manager overlay
/// (`BookingBottomSheet.kt:523-527` → `AddressManagerScreen`). Two panes: the
/// saved-address list (identical rendering to `AddressManagerView`, default
/// badged) and the existing map picker for a brand-new address.
struct BookingSavedAddressChooserView: View {
    @StateObject private var model: BookingSavedAddressChooserModel
    private let currentSavedAddressId: String?
    private let geocoding: GeocodingService
    private let mapProvider: MapProvider
    private let onPickSaved: (SavedAddress) -> Void
    private let onPickNew: (GeocodedAddress) -> Void
    private let onDismiss: () -> Void

    @State private var pane: Pane = .list

    private enum Pane {
        case list
        case map
    }

    init(
        repository: SavedAddressRepository?,
        currentSavedAddressId: String?,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        onPickSaved: @escaping (SavedAddress) -> Void,
        onPickNew: @escaping (GeocodedAddress) -> Void,
        onDismiss: @escaping () -> Void
    ) {
        _model = StateObject(wrappedValue: BookingSavedAddressChooserModel(repository: repository))
        self.currentSavedAddressId = currentSavedAddressId
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.onPickSaved = onPickSaved
        self.onPickNew = onPickNew
        self.onDismiss = onDismiss
    }

    private var preselectedId: String? {
        BookingSavedAddressSelection.preselectedId(
            addresses: model.addresses,
            currentSavedAddressId: currentSavedAddressId,
            repoSelectedId: model.selectedId
        )
    }

    var body: some View {
        content
    }

    @ViewBuilder
    private var content: some View {
        switch pane {
        case .list:
            SavedAddressListPane(
                addresses: model.addresses,
                preselectedId: preselectedId,
                loading: model.loading,
                onBack: onDismiss,
                onSelect: onPickSaved,
                onAddNew: { pane = .map }
            )
            .task { await model.onAppear() }
        case .map:
            BookingAddressPickerView(
                geocoding: geocoding,
                mapProvider: mapProvider,
                onConfirmed: onPickNew,
                onBack: { pane = .list }
            )
        }
    }
}

private struct SavedAddressListPane: View {
    let addresses: [SavedAddress]
    let preselectedId: String?
    let loading: Bool
    let onBack: () -> Void
    let onSelect: (SavedAddress) -> Void
    let onAddNew: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            header
            ScrollView {
                VStack(spacing: Spacing.s) {
                    if addresses.isEmpty, !loading {
                        emptyState
                    } else {
                        ForEach(addresses) { address in
                            ChooserAddressRow(
                                address: address,
                                isSelected: address.id == preselectedId,
                                onSelect: { onSelect(address) }
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
    }

    private var header: some View {
        HStack(spacing: Spacing.xs) {
            Button(action: onBack) {
                Image(systemName: "chevron.left")
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(CleansiaColors.onBackground)
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel(L10n.AddressManager.back)
            Text(L10n.AddressManager.title)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Spacer()
        }
        .padding(.horizontal, Spacing.xs)
        .padding(.top, Spacing.m)
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
        Button(action: onAddNew) {
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

private struct ChooserAddressRow: View {
    let address: SavedAddress
    let isSelected: Bool
    let onSelect: () -> Void

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
            if isSelected {
                Image(systemName: "checkmark")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundColor(CleansiaColors.primary)
            }
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

#if DEBUG
    struct BookingSavedAddressChooserView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSavedAddressChooserView(
                repository: nil,
                currentSavedAddressId: nil,
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider(),
                onPickSaved: { _ in },
                onPickNew: { _ in },
                onDismiss: {}
            )
        }
    }
#endif
