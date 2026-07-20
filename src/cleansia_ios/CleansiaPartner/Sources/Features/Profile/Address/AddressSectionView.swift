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

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.address,
            isLoading: vm.state.isLoading,
            header: {
                if onboarding {
                    OnboardingChainHeader(currentSection: .address, state: chainVM.state)
                }
            },
            form: {
                AddressSummaryCard(
                    line1: vm.summaryLine1,
                    line2: vm.summaryLine2,
                    enabled: !vm.action.isSubmitting,
                    onTap: { pickerOpen = true }
                )
                if vm.serviceAreaStatus == .countryNotServiced {
                    CountryNotServicedRow()
                }
                WhyWeNeedThisCard(expanded: $whyExpanded)
                SaveSectionButton(
                    onboarding: onboarding,
                    isSubmitting: vm.action.isSubmitting,
                    enabled: vm.canSave,
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

private struct CountryNotServicedRow: View {
    var body: some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundColor(CleansiaColors.error)
            Text(L10n.Profile.errorCountryNotServiced)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.error)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.error.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
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
