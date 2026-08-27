import CleansiaCore
import SwiftUI

struct AddressSectionView: View {
    @StateObject private var vm: AddressSectionViewModel
    @ObservedObject private var chainVM: OnboardingChainViewModel
    @State private var pickerOpen = false
    @State private var whyExpanded = false

    private let onboarding: Bool
    private let onSaved: () -> Void
    private let geocoding: GeocodingService
    private let mapProvider: MapProvider
    private let serviceArea: ServiceAreaProvider

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        chainVM: OnboardingChainViewModel,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider,
        onboarding: Bool,
        onSaved: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: AddressSectionViewModel(
            client: client,
            serviceArea: serviceArea,
            snackbar: snackbar
        ))
        self.chainVM = chainVM
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    /// Back is offered only inside the chain, and only when a previous step exists. Reuses the same
    /// jump the step dots added — replace, never push — so the two ways back behave identically.
    private var onboardingBack: (() -> Void)? {
        guard onboarding, let previous = ProfileSection.address.previous else { return nil }
        return { chainVM.requestJump(to: previous) }
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.address,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            header: {
                if onboarding {
                    OnboardingChainHeader(
                        currentSection: .address,
                        state: chainVM.state,
                        onSelect: { chainVM.requestJump(to: $0) }
                    )
                }
            },
            form: {
                AddressSummaryCard(
                    line1: vm.summaryLine1,
                    line2: vm.summaryLine2,
                    enabled: !vm.action.isSubmitting,
                    onTap: { pickerOpen = true }
                )
                // Drawn whenever an address is picked, in every state — see ServiceAreaRow.
                if vm.picked != nil {
                    ServiceAreaRow(status: vm.serviceAreaStatus)
                }
                WhyWeNeedThisCard(expanded: $whyExpanded)
                SaveSectionButton(
                    onboarding: onboarding,
                    isSubmitting: vm.action.isSubmitting,
                    enabled: vm.canSave,
                    onBack: onboardingBack,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
        .sheet(isPresented: $pickerOpen) {
            NavigationStack {
                AddressPickerView(
                    geocoding: geocoding,
                    mapProvider: mapProvider,
                    serviceArea: serviceArea,
                    onConfirmed: { address in
                        vm.applyPick(address)
                        pickerOpen = false
                    },
                    onBack: { pickerOpen = false }
                )
            }
        }
    }
}

private struct AddressSummaryCard: View {
    let line1: String?
    let line2: String?
    let enabled: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.10))
                        .frame(width: 40, height: 40)
                    Image(systemName: "mappin.and.ellipse")
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    if let line1 {
                        Text(line1)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                            .lineLimit(1)
                        if let line2 {
                            Text(line2)
                                .font(CleansiaTypography.labelSmall)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .lineLimit(1)
                        }
                    } else {
                        Text(L10n.Profile.addressPickOnMap)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        Text(L10n.Profile.addressPickOnMapHelper)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity)
            .background(CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.outline, lineWidth: 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }
}

/// The service-area verdict for the picked address, in all four of its states.
///
/// It used to render for exactly one of three, so a cleaner who picked an address in a serviced
/// country but an unserviced city — or one the app could not check at all — saw nothing and had
/// no way to tell a pass from a failed lookup. Android has always drawn all four.
///
/// UNKNOWN is deliberately shown, and deliberately neutral: "we could not check" is not a refusal,
/// and silence would let it read as one.
private struct ServiceAreaRow: View {
    let status: ServiceAreaStatus

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Image(systemName: glyph)
                .foregroundColor(tint)
            Text(message)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(tint)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(tint.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }

    private var glyph: String {
        switch status {
        case .unknown: "info.circle"
        case .inServicedCity: "checkmark.circle"
        case .outsideServicedCity: "info.circle"
        case .countryNotServiced: "exclamationmark.triangle.fill"
        }
    }

    private var tint: Color {
        switch status {
        case .unknown: CleansiaColors.onSurfaceVariant
        case .inServicedCity: CleansiaColors.primary
        // iOS has no `tertiary`; warningStar is the app's amber, and this state is advisory
        // rather than a refusal — the cleaner can still work, just not at this address.
        case .outsideServicedCity: CleansiaColors.warningStar
        case .countryNotServiced: CleansiaColors.error
        }
    }

    private var message: String {
        switch status {
        case .unknown: L10n.Profile.serviceAreaChecking
        case let .inServicedCity(city): L10n.Profile.serviceAreaInServicedCity(city)
        case .outsideServicedCity: L10n.Profile.serviceAreaOutsideServicedCity
        case .countryNotServiced: L10n.Profile.errorCountryNotServiced
        }
    }
}

private struct WhyWeNeedThisCard: View {
    @Binding var expanded: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Button {
                expanded.toggle()
            } label: {
                HStack(spacing: Spacing.s) {
                    Image(systemName: "questionmark.circle")
                        .foregroundColor(CleansiaColors.primary)
                    Text(L10n.Profile.addressWhyTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer()
                    Image(systemName: expanded ? "chevron.up" : "chevron.down")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .buttonStyle(.plain)

            if expanded {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    WhyRow(text: L10n.Profile.addressWhyReasonJobs)
                    WhyRow(text: L10n.Profile.addressWhyReasonDistancePay)
                    WhyRow(text: L10n.Profile.addressWhyReasonInvoice)
                    Text(L10n.Profile.addressWhyPrivacy)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .padding(.top, Spacing.xxs)
                }
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surfaceVariant.opacity(0.4))
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct WhyRow: View {
    let text: String

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Text(verbatim: "•")
                .foregroundColor(CleansiaColors.primary)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }
}
