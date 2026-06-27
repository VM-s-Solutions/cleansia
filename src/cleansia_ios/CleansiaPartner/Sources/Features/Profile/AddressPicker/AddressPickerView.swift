import CleansiaCore
import MapKit
import SwiftUI

struct AddressPickerView: View {
    @StateObject private var vm: AddressPickerViewModel
    @State private var region = MKCoordinateRegion(
        center: CLLocationCoordinate2D(latitude: defaultLatitude, longitude: defaultLongitude),
        span: MKCoordinateSpan(latitudeDelta: defaultSpan, longitudeDelta: defaultSpan)
    )
    @FocusState private var searchFocused: Bool

    private let mapProvider: MapProvider
    private let onConfirmed: (GeocodedAddress) -> Void
    private let onBack: () -> Void

    private static let defaultLatitude = 50.0755
    private static let defaultLongitude = 14.4378
    private static let defaultSpan = 0.012

    init(
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        onConfirmed: @escaping (GeocodedAddress) -> Void,
        onBack: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: AddressPickerViewModel(geocoding: geocoding))
        self.mapProvider = mapProvider
        self.onConfirmed = onConfirmed
        self.onBack = onBack
    }

    var body: some View {
        ZStack {
            mapProvider.pickerMap(region: $region, showsUserLocation: false)
                .ignoresSafeArea()
                .onChange(of: region.center.latitude) { _ in pushCenter() }
                .onChange(of: region.center.longitude) { _ in pushCenter() }

            CenterPin()

            VStack(spacing: 0) {
                topBar
                Spacer()
                ConfirmCard(
                    resolved: vm.resolved,
                    lookingUp: vm.lookingUp,
                    enabled: vm.canConfirm,
                    onConfirm: vm.confirm
                )
            }
        }
        .navigationBarHidden(true)
        .onReceive(vm.recenter) { coordinate in
            region.center = CLLocationCoordinate2D(latitude: coordinate.latitude, longitude: coordinate.longitude)
        }
        .onReceive(vm.confirmed, perform: onConfirmed)
    }

    private var topBar: some View {
        VStack(spacing: Spacing.xs) {
            HStack(spacing: Spacing.s) {
                FloatingCircleButton(
                    systemIcon: "chevron.left",
                    accessibilityLabel: L10n.AddressPicker.back,
                    action: onBack
                )
                SearchField(
                    query: vm.searchQuery,
                    focused: $searchFocused,
                    onChange: vm.onSearchChange,
                    onClear: vm.clearSearch
                )
            }
            if searchFocused, vm.searchQuery.count >= 2 {
                SearchDropdown(
                    searching: vm.searching,
                    results: vm.searchResults,
                    onSelect: { result in
                        vm.selectResult(result)
                        searchFocused = false
                    }
                )
            }
        }
        .padding(.horizontal, Spacing.s)
        .padding(.top, Spacing.s)
    }

    private func pushCenter() {
        vm.centerChanged(Coordinate(latitude: region.center.latitude, longitude: region.center.longitude))
    }
}

private struct CenterPin: View {
    var body: some View {
        VStack(spacing: 0) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.primary)
                    .frame(width: 28, height: 28)
                Circle()
                    .fill(CleansiaColors.onPrimary)
                    .frame(width: 10, height: 10)
            }
            Rectangle()
                .fill(CleansiaColors.primary)
                .frame(width: 2, height: 14)
            Spacer().frame(height: 24)
        }
    }
}

private struct FloatingCircleButton: View {
    let systemIcon: String
    let accessibilityLabel: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: systemIcon)
                .font(.system(size: 18, weight: .semibold))
                .foregroundColor(CleansiaColors.onSurface)
                .frame(width: 44, height: 44)
                .background(CleansiaColors.surface)
                .clipShape(Circle())
                .shadow(color: .black.opacity(0.15), radius: 6, y: 2)
        }
        .accessibilityLabel(accessibilityLabel)
    }
}

private struct SearchField: View {
    let query: String
    @FocusState.Binding var focused: Bool
    let onChange: (String) -> Void
    let onClear: () -> Void

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "magnifyingglass")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            TextField(
                L10n.AddressPicker.searchHint,
                text: Binding(get: { query }, set: onChange)
            )
            .focused($focused)
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurface)
            .submitLabel(.search)
            if !query.isEmpty {
                Button(action: onClear) {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .accessibilityLabel(L10n.AddressPicker.clear)
            }
        }
        .padding(.horizontal, Spacing.s)
        .frame(height: 44)
        .background(CleansiaColors.surface)
        .clipShape(Capsule())
        .shadow(color: .black.opacity(0.1), radius: 4, y: 1)
    }
}

private struct SearchDropdown: View {
    let searching: Bool
    let results: [GeocodedAddress]
    let onSelect: (GeocodedAddress) -> Void

    var body: some View {
        VStack(spacing: 0) {
            if searching {
                SearchStateRow(text: L10n.AddressPicker.searching, showProgress: true)
            } else if results.isEmpty {
                SearchStateRow(text: L10n.AddressPicker.noResults, showProgress: false)
            } else {
                ForEach(results.indices, id: \.self) { index in
                    SearchResultRow(result: results[index]) { onSelect(results[index]) }
                }
            }
        }
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        .shadow(color: .black.opacity(0.15), radius: 12, y: 4)
    }
}

private struct SearchStateRow: View {
    let text: String
    let showProgress: Bool

    var body: some View {
        HStack(spacing: Spacing.s) {
            if showProgress {
                ProgressView().scaleEffect(0.8)
            }
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.s)
    }
}

private struct SearchResultRow: View {
    let result: GeocodedAddress
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "mappin.circle")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                VStack(alignment: .leading, spacing: 2) {
                    Text(primaryLine)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                    if let secondLine {
                        Text(secondLine)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .lineLimit(1)
                    }
                }
                Spacer()
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
        }
        .buttonStyle(.plain)
    }

    private var primaryLine: String {
        result.street.isEmpty ? String(result.formatted.prefix(while: { $0 != "," })) : result.street
    }

    private var secondLine: String? {
        let parts = [result.zipCode, result.city, result.country].filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }
}

private struct ConfirmCard: View {
    let resolved: GeocodedAddress?
    let lookingUp: Bool
    let enabled: Bool
    let onConfirm: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "mappin.and.ellipse")
                    .foregroundColor(CleansiaColors.primary)
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                    if let subtitle {
                        Text(subtitle)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .lineLimit(1)
                    }
                }
                Spacer()
                if lookingUp {
                    ProgressView().scaleEffect(0.8)
                }
            }
            CleansiaPrimaryButton(L10n.AddressPicker.confirm, enabled: enabled, action: onConfirm)
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        .shadow(color: .black.opacity(0.15), radius: 16, y: -2)
        .padding(.horizontal, Spacing.s)
        .padding(.bottom, Spacing.xs)
    }

    private var title: String {
        if lookingUp { return L10n.AddressPicker.lookingUp }
        guard let resolved else { return L10n.AddressPicker.dragToPick }
        return resolved.street.isEmpty ? resolved.formatted : resolved.street
    }

    private var subtitle: String? {
        guard let resolved, !lookingUp else { return nil }
        let parts = [resolved.zipCode, resolved.city, resolved.country].filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }
}

#if DEBUG
    private struct PreviewMapProvider: MapProvider {
        func pickerMap(region _: Binding<MKCoordinateRegion>, showsUserLocation _: Bool) -> AnyView {
            AnyView(CleansiaColors.surfaceVariant)
        }
    }

    private func previewAddress() -> GeocodedAddress {
        GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Vinohradská 12, Praha"
        )
    }

    struct AddressPickerView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                ZStack {
                    PreviewMapProvider().pickerMap(region: .constant(sampleRegion), showsUserLocation: false)
                        .ignoresSafeArea()
                    CenterPin()
                    VStack {
                        Spacer()
                        ConfirmCard(resolved: nil, lookingUp: false, enabled: false, onConfirm: {})
                    }
                }
                .previewDisplayName("Empty · drag to pick")

                ZStack {
                    PreviewMapProvider().pickerMap(region: .constant(sampleRegion), showsUserLocation: false)
                        .ignoresSafeArea()
                    CenterPin()
                    VStack {
                        Spacer()
                        ConfirmCard(resolved: previewAddress(), lookingUp: false, enabled: true, onConfirm: {})
                    }
                }
                .previewDisplayName("Resolved · confirm enabled")
            }
        }

        private static let sampleRegion = MKCoordinateRegion(
            center: CLLocationCoordinate2D(latitude: 50.0755, longitude: 14.4378),
            span: MKCoordinateSpan(latitudeDelta: 0.012, longitudeDelta: 0.012)
        )
    }
#endif
