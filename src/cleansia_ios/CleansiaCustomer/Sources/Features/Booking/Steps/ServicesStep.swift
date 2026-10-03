import CleansiaCore
import SwiftUI

struct ServicesStep: View {
    @ObservedObject var viewModel: BookingViewModel

    var body: some View {
        Group {
            switch viewModel.catalogState {
            case .loading:
                CatalogMessageView(
                    systemImage: "arrow.triangle.2.circlepath",
                    message: L10n.Booking.catalogLoading,
                    showsSpinner: true
                )
            case .error:
                CatalogMessageView(
                    systemImage: "arrow.clockwise",
                    message: L10n.Booking.catalogError,
                    retryTitle: L10n.Booking.catalogRetry,
                    onRetry: { Task { await viewModel.retryCatalog() } }
                )
            case let .loaded(catalog) where catalog.isEmpty:
                CatalogMessageView(
                    systemImage: "bubbles.and.sparkles",
                    message: L10n.Booking.catalogEmpty,
                    retryTitle: L10n.Booking.catalogRetry,
                    onRetry: { Task { await viewModel.retryCatalog() } }
                )
            case let .loaded(catalog):
                CatalogContentView(
                    catalog: catalog,
                    state: viewModel.state,
                    onUpdate: { transform in viewModel.update(transform) },
                    onRoomsChange: viewModel.setRooms,
                    onBathroomsChange: viewModel.setBathrooms
                )
            }
        }
        .task { await viewModel.loadCatalog() }
    }
}

private struct CatalogContentView: View {
    @Environment(\.locale) private var locale
    let catalog: Catalog
    let state: BookingState
    let onUpdate: ((BookingState) -> BookingState) -> Void
    let onRoomsChange: (Int) -> Void
    let onBathroomsChange: (Int) -> Void

    @State private var activeCategorySlug: String?
    @State private var detailPackage: CatalogPackage?

    private var categories: [CatalogCategory] {
        var seen = Set<String>()
        return catalog.services
            .map(\.category)
            .filter { seen.insert($0.slug).inserted }
            .sorted { $0.displayOrder < $1.displayOrder }
    }

    private var filteredServices: [CatalogService] {
        guard let activeCategorySlug else { return catalog.services }
        return catalog.services.filter { $0.category.slug == activeCategorySlug }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.ml) {
                PropertyRow(
                    rooms: state.rooms,
                    bathrooms: state.bathrooms,
                    onRoomsChange: onRoomsChange,
                    onBathroomsChange: onBathroomsChange
                )
                .padding(.horizontal, Spacing.ml)

                if !catalog.packages.isEmpty {
                    packagesSection
                }

                servicesSection
            }
            .padding(.vertical, Spacing.s)
        }
        .sheet(item: $detailPackage) { pkg in
            PackageDetailsSheet(
                pkg: pkg,
                currencyCode: catalog.currencyCode,
                isSelected: state.selectedPackageIds.contains(pkg.id),
                onToggle: { togglePackage(pkg.id) },
                onDismiss: { detailPackage = nil }
            )
        }
    }

    private var packagesSection: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            SectionHeader(L10n.Booking.packagesFeatured)
                .padding(.horizontal, Spacing.ml)
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: Spacing.s) {
                    ForEach(Array(catalog.packages.enumerated()), id: \.element.id) { index, pkg in
                        PackageCard(
                            pkg: pkg,
                            currencyCode: catalog.currencyCode,
                            accent: PackageAccent.gradient(for: index),
                            selected: state.selectedPackageIds.contains(pkg.id),
                            onOpen: { detailPackage = pkg }
                        )
                    }
                }
                .padding(.horizontal, Spacing.ml)
            }
        }
    }

    private var servicesSection: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            SectionHeader(L10n.Booking.pickService)
                .padding(.horizontal, Spacing.ml)

            if categories.count > 1 {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: Spacing.xs) {
                        CategoryChip(
                            label: L10n.Booking.catAll,
                            systemImage: "star",
                            tint: CategoryPalette.defaultTint,
                            selected: activeCategorySlug == nil
                        ) {
                            activeCategorySlug = nil
                        }
                        ForEach(categories) { category in
                            CategoryChip(
                                label: category.localizedName(for: locale),
                                systemImage: CategoryPalette.symbol(for: category.slug),
                                tint: CategoryPalette.tint(for: category.slug),
                                selected: activeCategorySlug == category.slug
                            ) {
                                activeCategorySlug = category.slug
                            }
                        }
                    }
                    .padding(.horizontal, Spacing.ml)
                }
            }

            if filteredServices.isEmpty {
                EmptyResults()
            } else {
                ForEach(filteredServices) { service in
                    ServiceRow(
                        service: service,
                        currencyCode: catalog.currencyCode,
                        selected: state.selectedServiceIds.contains(service.id),
                        onToggle: { toggleService(service.id) }
                    )
                    .padding(.horizontal, Spacing.ml)
                }
            }
        }
    }

    private func toggleService(_ id: String) {
        onUpdate { mutate($0) { state in
            if state.selectedServiceIds.contains(id) {
                state.selectedServiceIds.remove(id)
            } else {
                state.selectedServiceIds.insert(id)
            }
        } }
    }

    private func togglePackage(_ id: String) {
        onUpdate { mutate($0) { state in
            if state.selectedPackageIds.contains(id) {
                state.selectedPackageIds.remove(id)
            } else {
                state.selectedPackageIds.insert(id)
            }
        } }
    }

    private func mutate(_ state: BookingState, _ change: (inout BookingState) -> Void) -> BookingState {
        var next = state
        change(&next)
        return next
    }
}

private struct PropertyRow: View {
    let rooms: Int
    let bathrooms: Int
    let onRoomsChange: (Int) -> Void
    let onBathroomsChange: (Int) -> Void

    /// The caption states the caps up front, so the plus greying at them reads as the limit it is. The
    /// title sits above the steppers, not beside them: on a 320 pt phone the two uk steppers ("3 кімнати",
    /// "2 ванні кімнати") need more than the row's width on their own, and beside them the title was
    /// squeezed to nothing. A stepper that still does not fit wraps its label (Android's twin does both).
    /// The two split the row equally, 8 pt apart as on the quick-size slide, from the title's leading
    /// edge to the card's trailing one, and stay one height when a label wraps.
    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.Booking.yourHome)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.primary)
                .padding(.bottom, Spacing.hair)
            HStack(spacing: Spacing.xs) {
                PropertyStepper(
                    name: L10n.Booking.yourHome,
                    label: L10n.Booking.roomsShort(rooms),
                    value: rooms,
                    minimum: 1,
                    maximum: PropertySize.maxRooms,
                    fills: true,
                    onChange: onRoomsChange
                )
                PropertyStepper(
                    name: L10n.Booking.yourHome,
                    label: L10n.Booking.bathShort(bathrooms),
                    value: bathrooms,
                    minimum: 1,
                    maximum: PropertySize.maxBathrooms,
                    fills: true,
                    onChange: onBathroomsChange
                )
            }
            .fixedSize(horizontal: false, vertical: true)
            Text(L10n.Booking.sizeLimitCaption)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, Spacing.s)
        .padding(.vertical, Spacing.xs)
        .background(CleansiaColors.primaryContainer.opacity(0.5))
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

#if DEBUG
    struct ServicesStep_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                CatalogContentPreview(catalog: CatalogFixturesPreview.populated)
                    .previewDisplayName("Loaded")
                CatalogMessageView(
                    systemImage: "arrow.triangle.2.circlepath",
                    message: L10n.Booking.catalogLoading,
                    showsSpinner: true
                )
                .previewDisplayName("Loading")
                CatalogMessageView(
                    systemImage: "arrow.clockwise",
                    message: L10n.Booking.catalogError,
                    retryTitle: L10n.Booking.catalogRetry,
                    onRetry: {}
                )
                .previewDisplayName("Error")
            }
        }
    }

    private struct CatalogContentPreview: View {
        let catalog: Catalog
        @State private var state = BookingState()

        var body: some View {
            CatalogContentView(
                catalog: catalog,
                state: state,
                onUpdate: { transform in state = transform(state) },
                onRoomsChange: { state.rooms = $0 },
                onBathroomsChange: { state.bathrooms = $0 }
            )
            .background(CleansiaColors.background)
        }
    }

    private enum CatalogFixturesPreview {
        static let populated = Catalog(
            services: [
                CatalogService(
                    id: "s-1",
                    name: "Standard cleaning",
                    description: "Everyday tidy-up",
                    basePrice: 500,
                    perRoomPrice: 100,
                    category: home,
                    translations: [:]
                ),
                CatalogService(
                    id: "s-2",
                    name: "Deep cleaning",
                    description: "Thorough clean",
                    basePrice: 900,
                    perRoomPrice: 150,
                    category: deep,
                    translations: [:]
                )
            ],
            packages: [
                CatalogPackage(
                    id: "p-1",
                    name: "Move-out",
                    description: "Top to bottom",
                    price: 2500,
                    translations: [:],
                    includedServices: []
                )
            ],
            currencyCode: "CZK",
            defaultCurrencyCode: "CZK"
        )
        static let home = CatalogCategory(
            id: "c-home",
            slug: "home",
            name: "Home",
            description: nil,
            displayOrder: 0,
            translations: [:]
        )
        static let deep = CatalogCategory(
            id: "c-deep",
            slug: "deep",
            name: "Deep",
            description: nil,
            displayOrder: 1,
            translations: [:]
        )
    }
#endif
