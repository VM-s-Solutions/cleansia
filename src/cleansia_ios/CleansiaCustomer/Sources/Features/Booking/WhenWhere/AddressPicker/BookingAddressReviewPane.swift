import CleansiaCore
import SwiftUI

/// Mirrors the Android booking new-address save decision
/// (`AddressManagerScreen.kt:199-212` `ReviewPane.onConfirm`): a saved pick
/// persists a draft (label falls back to the picked city, then a generic
/// fallback); the one-off path returns nil so nothing is persisted. In both
/// cases the address is still applied to the order inline, matching Android
/// (`BookingBottomSheet.kt:661-672`, where the freshly-picked address carries no
/// `serverId`, so the current order never sends a `savedAddressId`).
enum BookingNewAddressSave {
    static func draft(
        from address: GeocodedAddress,
        label: String,
        save: Bool,
        setAsDefault: Bool,
        fallbackLabel: String
    ) -> SavedAddressDraft? {
        guard save else { return nil }
        let trimmed = label.trimmingCharacters(in: .whitespacesAndNewlines)
        let resolved = trimmed.isEmpty ? fallbackLabel : trimmed
        return address.toDraft(label: resolved, setAsDefault: setAsDefault)
    }
}

private struct BookingAddressSaveOfferedKey: EnvironmentKey {
    static let defaultValue = false
}

extension EnvironmentValues {
    /// Set by `WhenWhereStep` on the booking saved-address chooser so its map
    /// picker offers to persist a brand-new address; the Address Manager hosts
    /// the same picker without it (that flow has its own review pane).
    var bookingAddressSaveOffered: Bool {
        get { self[BookingAddressSaveOfferedKey.self] }
        set { self[BookingAddressSaveOfferedKey.self] = newValue }
    }
}

struct BookingAddressReviewPane: View {
    let picked: GeocodedAddress
    let saving: Bool
    let onBack: () -> Void
    let onConfirm: (String, Bool, Bool) -> Void

    @State private var label: String
    @State private var save = true
    @State private var setAsDefault = false

    init(
        picked: GeocodedAddress,
        saving: Bool,
        onBack: @escaping () -> Void,
        onConfirm: @escaping (String, Bool, Bool) -> Void
    ) {
        self.picked = picked
        self.saving = saving
        self.onBack = onBack
        self.onConfirm = onConfirm
        _label = State(initialValue: picked.city.isBlank ? L10n.AddressManager.fallbackLabel : picked.city)
    }

    var body: some View {
        VStack(spacing: 0) {
            header
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
            CleansiaPrimaryButton(L10n.AddressManager.confirm, enabled: !saving) {
                onConfirm(label, save, setAsDefault)
            }
            .padding(.horizontal, Spacing.l)
            .padding(.bottom, Spacing.m)
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
            Text(L10n.AddressManager.reviewTitle)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Spacer()
        }
        .padding(.horizontal, Spacing.xs)
        .padding(.top, Spacing.m)
    }

    private var addressCard: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(picked.street.isBlank ? picked.formatted : picked.street)
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
    struct BookingAddressReviewPane_Previews: PreviewProvider {
        static var previews: some View {
            BookingAddressReviewPane(
                picked: GeocodedAddress(
                    latitude: 50.0755,
                    longitude: 14.4378,
                    street: "Wenceslas Square 1",
                    city: "Prague",
                    zipCode: "11000",
                    country: "Czechia",
                    countryIsoCode: "cz",
                    formatted: "Wenceslas Square 1, Prague"
                ),
                saving: false,
                onBack: {},
                onConfirm: { _, _, _ in }
            )
            .background(CleansiaColors.background)
        }
    }
#endif
