import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct IdentificationSectionView: View {
    @StateObject private var vm: IdentificationSectionViewModel
    @ObservedObject private var chainVM: OnboardingChainViewModel
    private let onboarding: Bool
    private let onSaved: () -> Void

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        chainVM: OnboardingChainViewModel,
        onboarding: Bool,
        onSaved: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: IdentificationSectionViewModel(client: client, snackbar: snackbar))
        self.chainVM = chainVM
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.identification,
            isLoading: vm.state.isLoading,
            header: {
                if onboarding {
                    OnboardingChainHeader(currentSection: .identification, state: chainVM.state)
                }
            },
            form: {
                CleansiaDropdown(
                    selectedId: $vm.form.nationalityId,
                    options: vm.countryOptions,
                    label: L10n.Profile.nationality,
                    placeholder: L10n.Profile.noData,
                    searchable: true
                )
                CleansiaTextField(
                    value: $vm.form.passportId,
                    label: L10n.Profile.passport
                )
                EntityTypePicker(
                    selected: vm.form.entityType,
                    onSelect: vm.setEntityType
                )
                CleansiaDropdown(
                    selectedId: $vm.form.businessCountryId,
                    options: vm.countryOptions,
                    label: L10n.Profile.businessCountry,
                    placeholder: L10n.Profile.noData,
                    searchable: true
                )
                CleansiaTextField(
                    value: $vm.form.registrationNumber,
                    label: L10n.Profile.registrationNumber
                )
                CleansiaTextField(
                    value: $vm.form.vatNumber,
                    label: L10n.Profile.vatNumber
                )
                if vm.isLegalEntity {
                    CleansiaTextField(
                        value: $vm.form.legalEntityName,
                        label: L10n.Profile.legalEntityName
                    )
                }
                SaveSectionButton(
                    onboarding: onboarding,
                    isSubmitting: vm.action.isSubmitting,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
    }
}

private struct EntityTypePicker: View {
    let selected: EmployeeEntityType
    let onSelect: (EmployeeEntityType) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Profile.entityType)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            HStack(spacing: Spacing.s) {
                EntityChip(
                    label: L10n.Profile.entityTypeNatural,
                    isSelected: selected == ._1,
                    action: { onSelect(._1) }
                )
                EntityChip(
                    label: L10n.Profile.entityTypeLegal,
                    isSelected: selected == ._2,
                    action: { onSelect(._2) }
                )
            }
        }
    }
}

private struct EntityChip: View {
    let label: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(isSelected ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                .padding(.horizontal, Spacing.m)
                .frame(maxWidth: .infinity, minHeight: 44)
                .background(isSelected ? CleansiaColors.primary : CleansiaColors.surface)
                .overlay(
                    Capsule().stroke(
                        isSelected ? Color.clear : CleansiaColors.outline,
                        lineWidth: 1
                    )
                )
                .clipShape(Capsule())
        }
        .buttonStyle(.plain)
    }
}
