import MapKit
import SwiftUI

/// What the address picker says. Each app words it in its own catalog.
public protocol AddressPickerCopy {
    static var back: String { get }
    static var searchHint: String { get }
    static var clear: String { get }
    static var searching: String { get }
    static var noResults: String { get }
    static var myLocation: String { get }
    static var locationDenied: String { get }
    static var locationUnavailable: String { get }
    static var lookingUp: String { get }
    static var dragToPick: String { get }
    static var confirm: String { get }
}

/// The map that picks an address: a pin over the map, the search field, the my-location button and the card
/// that confirms the address under the pin. A caller with a step after the pick passes it as `review`: it takes
/// the map's place until it goes back, and the map keeps its place and its address meanwhile.
public struct AddressPickerScreen<Review: View>: View {
    @StateObject private var vm: AddressPickerViewModel
    /// Prague, until a location fix or a pick moves the map.
    @State private var region = MKCoordinateRegion(
        center: CLLocationCoordinate2D(latitude: 50.0755, longitude: 14.4378),
        span: MKCoordinateSpan(latitudeDelta: 0.012, longitudeDelta: 0.012)
    )
    @FocusState private var searchFocused: Bool
    @State private var reviewing: GeocodedAddress?
    @State private var bottomCover: CGFloat = 0
    @Environment(\.locationProvider) private var location
    @Environment(\.snackbarController) private var snackbar

    private let mapProvider: MapProvider
    private let copy: AddressPickerCopy.Type
    private let review: ((GeocodedAddress, @escaping () -> Void) -> Review)?
    private let onConfirmed: (GeocodedAddress) -> Void
    private let onBack: () -> Void

    public init(
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider?,
        copy: AddressPickerCopy.Type,
        review: ((GeocodedAddress, @escaping () -> Void) -> Review)?,
        onConfirmed: @escaping (GeocodedAddress) -> Void,
        onBack: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: AddressPickerViewModel(
            geocoding: geocoding,
            servicedCountryCodesProvider: serviceArea.map { provider in
                { await provider.servicedCountryIsoCodes() }
            }
        ))
        self.mapProvider = mapProvider
        self.copy = copy
        self.review = review
        self.onConfirmed = onConfirmed
        self.onBack = onBack
    }

    public var body: some View {
        Group {
            if let reviewing, let review {
                review(reviewing) { self.reviewing = nil }
            } else {
                mapContent
            }
        }
        .navigationBarHidden(true)
    }

    private var mapContent: some View {
        ZStack {
            mapProvider.pickerMap(region: $region, showsUserLocation: false, bottomInset: bottomCover)
                .ignoresSafeArea()
                .onChange(of: region.center.latitude) { _ in pushCenter() }
                .onChange(of: region.center.longitude) { _ in pushCenter() }

            VStack(spacing: 0) {
                topBar
                Spacer()
                VStack(spacing: 0) {
                    HStack {
                        Spacer()
                        FloatingCircleButton(
                            systemIcon: "location",
                            accessibilityLabel: copy.myLocation,
                            action: { Task { await vm.recenterOnMyLocation(location: location) } }
                        )
                    }
                    .padding(.horizontal, Spacing.s)
                    .padding(.bottom, Spacing.s)
                    AddressPickerConfirmCard(
                        resolved: vm.resolved,
                        lookingUp: vm.lookingUp,
                        enabled: vm.canConfirm,
                        copy: copy,
                        onConfirm: vm.confirm
                    )
                }
                // The map lifts Apple's logo and Legal link above everything laid over its bottom edge,
                // the location button included (Legal sits under it on iOS 16). The card grows with the
                // text size, so the cover is measured, not assumed.
                .background(GeometryReader { proxy in
                    Color.clear
                        .onAppear { bottomCover = proxy.size.height }
                        .onChange(of: proxy.size.height) { bottomCover = $0 }
                })
            }
        }
        .task { await vm.autoCenterOnOpen(location: location) }
        .onReceive(vm.recenter) { coordinate in
            region.center = CLLocationCoordinate2D(latitude: coordinate.latitude, longitude: coordinate.longitude)
        }
        .onReceive(vm.locationFailed) { failure in
            switch failure {
            case .denied:
                snackbar.showError(copy.locationDenied)
            case .unavailable:
                snackbar.showError(copy.locationUnavailable)
            }
        }
        .onReceive(vm.confirmed) { address in
            if review == nil {
                onConfirmed(address)
            } else {
                reviewing = address
            }
        }
    }

    private var topBar: some View {
        VStack(spacing: Spacing.xs) {
            HStack(spacing: Spacing.s) {
                FloatingCircleButton(
                    systemIcon: "chevron.left",
                    accessibilityLabel: copy.back,
                    action: onBack
                )
                SearchField(
                    query: vm.searchQuery,
                    focused: $searchFocused,
                    copy: copy,
                    onChange: vm.onSearchChange,
                    onClear: vm.clearSearch
                )
            }
            if searchFocused, vm.searchQuery.count >= 2 {
                SearchDropdown(
                    searching: vm.searching,
                    results: vm.searchResults,
                    copy: copy,
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

public extension AddressPickerScreen where Review == EmptyView {
    init(
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider?,
        copy: AddressPickerCopy.Type,
        onConfirmed: @escaping (GeocodedAddress) -> Void,
        onBack: @escaping () -> Void
    ) {
        self.init(
            geocoding: geocoding,
            mapProvider: mapProvider,
            serviceArea: serviceArea,
            copy: copy,
            review: nil,
            onConfirmed: onConfirmed,
            onBack: onBack
        )
    }
}

private struct SearchField: View {
    let query: String
    @FocusState.Binding var focused: Bool
    let copy: AddressPickerCopy.Type
    let onChange: (String) -> Void
    let onClear: () -> Void

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "magnifyingglass")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            TextField(
                copy.searchHint,
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
                .accessibilityLabel(copy.clear)
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
    let copy: AddressPickerCopy.Type
    let onSelect: (GeocodedAddress) -> Void

    var body: some View {
        VStack(spacing: 0) {
            if searching {
                SearchStateRow(text: copy.searching, showProgress: true)
            } else if results.isEmpty {
                SearchStateRow(text: copy.noResults, showProgress: false)
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

public struct AddressPickerConfirmCard: View {
    let resolved: GeocodedAddress?
    let lookingUp: Bool
    let enabled: Bool
    let copy: AddressPickerCopy.Type
    let onConfirm: () -> Void

    public init(
        resolved: GeocodedAddress?,
        lookingUp: Bool,
        enabled: Bool,
        copy: AddressPickerCopy.Type,
        onConfirm: @escaping () -> Void
    ) {
        self.resolved = resolved
        self.lookingUp = lookingUp
        self.enabled = enabled
        self.copy = copy
        self.onConfirm = onConfirm
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "mappin.and.ellipse")
                    .foregroundColor(CleansiaColors.primary)
                // Two lines tall in every state — the street over its city line — so the lookup that
                // starts on each drag and ends with an address never changes the card's height. The map
                // is inset by that height (its Legal link and the pin's centre sit above the card), and a
                // new inset re-centres it: the card growing and shrinking is what moved the map. While
                // there is no second line, the title holds its place unseen, so the height follows the
                // text size.
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                    Text(subtitle ?? title)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                        .opacity(subtitle == nil ? 0 : 1)
                        .accessibilityHidden(subtitle == nil)
                }
                Spacer()
                if lookingUp {
                    ProgressView().scaleEffect(0.8)
                }
            }
            CleansiaPrimaryButton(copy.confirm, enabled: enabled, action: onConfirm)
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        .shadow(color: .black.opacity(0.15), radius: 16, y: -2)
        .padding(.horizontal, Spacing.s)
        .padding(.bottom, Spacing.xs)
    }

    private var title: String {
        if lookingUp { return copy.lookingUp }
        guard let resolved else { return copy.dragToPick }
        return resolved.street.isEmpty ? resolved.formatted : resolved.street
    }

    private var subtitle: String? {
        guard let resolved, !lookingUp else { return nil }
        let parts = [resolved.zipCode, resolved.city, resolved.country].filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }
}

#if DEBUG
    private enum PreviewAddressPickerCopy: AddressPickerCopy {
        static let back = "Back"
        static let searchHint = "Search address…"
        static let clear = "Clear"
        static let searching = "Searching…"
        static let noResults = "No results"
        static let myLocation = "My location"
        static let locationDenied = "Location permission denied."
        static let locationUnavailable = "Couldn't get your location."
        static let lookingUp = "Looking up address…"
        static let dragToPick = "Drag the map to place the pin"
        static let confirm = "Use this address"
    }

    struct AddressPickerScreen_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                ZStack {
                    PreviewMapProvider().pickerMap(
                        region: .constant(sampleRegion),
                        showsUserLocation: false,
                        bottomInset: 0
                    )
                    .ignoresSafeArea()
                    VStack {
                        Spacer()
                        AddressPickerConfirmCard(
                            resolved: nil,
                            lookingUp: false,
                            enabled: false,
                            copy: PreviewAddressPickerCopy.self,
                            onConfirm: {}
                        )
                    }
                }
                .previewDisplayName("Empty · drag to pick")

                ZStack {
                    PreviewMapProvider().pickerMap(
                        region: .constant(sampleRegion),
                        showsUserLocation: false,
                        bottomInset: 0
                    )
                    .ignoresSafeArea()
                    VStack {
                        Spacer()
                        AddressPickerConfirmCard(
                            resolved: GeocodedAddress(
                                latitude: 50.0755,
                                longitude: 14.4378,
                                street: "Vinohradská 12",
                                city: "Praha",
                                zipCode: "120 00",
                                country: "Czechia",
                                countryIsoCode: "cz",
                                formatted: "Vinohradská 12, Praha"
                            ),
                            lookingUp: false,
                            enabled: true,
                            copy: PreviewAddressPickerCopy.self,
                            onConfirm: {}
                        )
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
