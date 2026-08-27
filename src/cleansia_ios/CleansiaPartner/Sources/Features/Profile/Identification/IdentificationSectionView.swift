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

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    /// Back is offered only inside the chain, and only when a previous step exists. Reuses the same
    /// jump the step dots added — replace, never push — so the two ways back behave identically.
    private var onboardingBack: (() -> Void)? {
        guard onboarding, let previous = ProfileSection.identification.previous else { return nil }
        return { chainVM.requestJump(to: previous) }
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.identification,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            header: {
                if onboarding {
                    OnboardingChainHeader(
                        currentSection: .identification,
                        state: chainVM.state,
                        onSelect: { chainVM.requestJump(to: $0) }
                    )
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
                    // The country's own word when it has one, our neutral wording when it does not.
                    // "Registration number" is correct everywhere and precise nowhere, which is
                    // exactly what a fallback should be — flattening every country to it would have
                    // cost CZ and SK the term their own registries use.
                    label: vm.fieldLabels?.registrationNumberLabel ?? L10n.Profile.registrationNumber
                )
                CleansiaTextField(
                    value: $vm.form.vatNumber,
                    label: vm.fieldLabels?.vatNumberLabel ?? L10n.Profile.vatNumber
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
                    onBack: onboardingBack,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        // The labels belong to the BUSINESS country, so they follow the picker rather than the
        // load. Android does the same off onBusinessCountrySelected.
        .onChange(of: vm.form.businessCountryId) { countryId in
            Task { await vm.loadFieldLabels(for: countryId) }
        }
        .onReceive(vm.saved) { onSaved() }
    }
}

/// Self-employed / Legal entity, as one track with a selection capsule that slides between the
/// two halves.
///
/// The shape is the Cleansia Plus plan switcher (`SubscribePlusScreen.PlanSwitcher`), retinted
/// from the membership palette to the app's own. That page is what the owner pointed at, and the
/// thing that reads as swipeable on it is the sliding capsule rather than an actual gesture —
/// neither Plus page pages, on either platform.
///
/// A real pager was considered and rejected. Both section scaffolds put the form inside a vertical
/// `ScrollView`, and a horizontally-paging container inside one fights the vertical scroll on
/// every diagonal drag; and with only `legalEntityName` differing between the two types, the two
/// panes would be all-but-identical.
///
/// A drag IS accepted, because a control that looks like it slides should answer a slide: it flips
/// the selection past a small threshold, rather than tracking the finger, so it cannot land the
/// capsule between two segments.
private struct EntityTypePicker: View {
    let selected: EmployeeEntityType
    let onSelect: (EmployeeEntityType) -> Void

    private static let height: CGFloat = 48
    private static let inset: CGFloat = 3

    /// Not `firstIndex(of:)` — the wire enum carries cases this control does not offer, and
    /// anything that is not Legal reads as Self-employed rather than as no selection at all.
    private var selectedIndex: Int {
        selected == ._2 ? 1 : 0
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Profile.entityType)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)

            GeometryReader { geometry in
                let segment = max((geometry.size.width - Self.inset * 2) / 2, 0)
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(CleansiaColors.primary)
                        .frame(width: segment)
                        .offset(x: segment * CGFloat(selectedIndex))
                        .animation(
                            .spring(response: 0.28, dampingFraction: 0.86),
                            value: selectedIndex
                        )

                    HStack(spacing: 0) {
                        segmentButton(L10n.Profile.entityTypeNatural, index: 0, type: ._1)
                        segmentButton(L10n.Profile.entityTypeLegal, index: 1, type: ._2)
                    }
                }
                .padding(Self.inset)
            }
            .frame(height: Self.height)
            .background(CleansiaColors.surfaceVariant, in: Capsule())
            .overlay(Capsule().stroke(CleansiaColors.outlineVariant, lineWidth: 1))
            .gesture(
                DragGesture(minimumDistance: 20)
                    .onEnded { value in
                        // Flips on direction, not on distance travelled: the capsule is bound to
                        // the selection, so tracking the finger could leave it mid-track.
                        if value.translation.width < 0, selectedIndex == 0 {
                            onSelect(._2)
                        } else if value.translation.width > 0, selectedIndex == 1 {
                            onSelect(._1)
                        }
                    }
            )
            .accessibilityElement(children: .contain)
        }
    }

    private func segmentButton(
        _ label: String,
        index: Int,
        type: EmployeeEntityType
    ) -> some View {
        Button {
            onSelect(type)
        } label: {
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(
                    selectedIndex == index ? CleansiaColors.onPrimary : CleansiaColors.onSurface
                )
                .lineLimit(1)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(selectedIndex == index ? .isSelected : [])
    }
}
