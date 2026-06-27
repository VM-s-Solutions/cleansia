import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct ProfileHubContent: View {
    let data: ProfileData
    let onOpen: (ProfileRoute) -> Void
    let onLogout: () -> Void

    private var employee: EmployeeItem {
        data.employee
    }

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                ProfileHero(employee: employee, contractStatus: data.contractStatus)

                SectionGroup(title: L10n.Profile.groupAccount) {
                    ProfileSectionRow(
                        icon: "person",
                        title: L10n.Profile.personal,
                        summary: displayName,
                        onTap: { onOpen(.personal(onboarding: false)) }
                    )
                    RowDivider()
                    ProfileSectionRow(
                        icon: "mappin.and.ellipse",
                        title: L10n.Profile.address,
                        summary: displayAddress,
                        onTap: { onOpen(.address(onboarding: false)) }
                    )
                    RowDivider()
                    ProfileSectionRow(
                        icon: "phone",
                        title: L10n.Profile.emergencyContact,
                        summary: displayEmergency,
                        onTap: { onOpen(.emergency) }
                    )
                }

                SectionGroup(title: L10n.Profile.groupWorkLegal) {
                    ProfileSectionRow(
                        icon: "person.text.rectangle",
                        title: L10n.Profile.identification,
                        summary: employee.passportId.nonBlankOrNil ?? L10n.Profile.noData,
                        onTap: { onOpen(.identification(onboarding: false)) }
                    )
                    RowDivider()
                    ProfileSectionRow(
                        icon: "building.columns",
                        title: L10n.Profile.bankDetails,
                        summary: employee.iban.nonBlankOrNil ?? L10n.Profile.noData,
                        onTap: { onOpen(.bank(onboarding: false)) }
                    )
                    RowDivider()
                    ProfileSectionRow(
                        icon: "doc.text",
                        title: L10n.Profile.myDocuments,
                        summary: L10n.Profile.documentsSummary,
                        onTap: { onOpen(.documents) }
                    )
                }

                LogoutRow(onTap: onLogout)
            }
            .padding(Spacing.m)
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }

    private var displayName: String {
        let value = [employee.firstName, employee.lastName]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: " ")
        return value.isEmpty ? L10n.Profile.noData : value
    }

    private var displayAddress: String {
        let value = [employee.street, employee.city, employee.zipCode]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: ", ")
        return value.isEmpty ? L10n.Profile.noData : value
    }

    private var displayEmergency: String {
        let value = [employee.emergencyContactName, employee.emergencyContactPhone]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: " · ")
        return value.isEmpty ? L10n.Profile.noData : value
    }
}

private struct ProfileHero: View {
    let employee: EmployeeItem
    let contractStatus: ContractStatus?

    var body: some View {
        HStack(spacing: Spacing.m) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.primaryContainer.opacity(0.4))
                    .frame(width: 80, height: 80)
                Text(initials)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.primary)
            }
            VStack(alignment: .leading, spacing: 4) {
                Text(name)
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                    .lineLimit(1)
                if let email = employee.email.nonBlankOrNil {
                    Text(email)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }
                if let contractStatus {
                    ContractStatusChip(status: contractStatus)
                }
            }
            Spacer()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var name: String {
        let value = [employee.firstName, employee.lastName]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: " ")
        return value.isEmpty ? L10n.Profile.noData : value
    }

    private var initials: String {
        let chars = [employee.firstName, employee.lastName]
            .compactMap { $0.nonBlankOrNil?.first }
            .map { String($0).uppercased() }
            .joined()
        return chars.isEmpty ? "?" : chars
    }
}

private struct ContractStatusChip: View {
    let status: ContractStatus

    var body: some View {
        HStack(spacing: 6) {
            Circle()
                .fill(content)
                .frame(width: 6, height: 6)
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(content)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 4)
        .background(container)
        .clipShape(Capsule())
    }

    /// ContractStatus raw cases: _1 Pending, _2 Active, _3 Terminated,
    /// _4 Approved, _5 Rejected (matches RegistrationCompletion aliases).
    private var label: String {
        switch status {
        case ._1: L10n.Profile.contractStatusPending
        case ._2: L10n.Profile.contractStatusActive
        case ._3: L10n.Profile.contractStatusTerminated
        case ._4: L10n.Profile.contractStatusApproved
        case ._5: L10n.Profile.contractStatusRejected
        }
    }

    private var container: Color {
        switch status {
        case ._2, ._4: CleansiaColors.successBg
        case ._1: Self.amberContainer
        case ._3, ._5: CleansiaColors.errorContainer
        }
    }

    private var content: Color {
        switch status {
        case ._2, ._4: CleansiaColors.successText
        case ._1: Self.amberContent
        case ._3, ._5: CleansiaColors.onErrorContainer
        }
    }

    // Material ships no warning/amber slot; parity with the Android
    // StatusAmber* hardcode (ProfileScreen.kt:388) for the Pending chip.
    private static let amberContainer = Color(red: 1.0, green: 0.91, blue: 0.76)
    private static let amberContent = Color(red: 0.48, green: 0.30, blue: 0.0)
}

private struct SectionGroup<Content: View>: View {
    let title: String
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(title.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.leading, Spacing.xs)
            VStack(spacing: 0) {
                content()
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
    }
}

private struct ProfileSectionRow: View {
    let icon: String
    let title: String
    let summary: String
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.12))
                        .frame(width: 32, height: 32)
                    Image(systemName: icon)
                        .font(.system(size: 16))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(summary)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s + 2)
        }
        .buttonStyle(.plain)
    }
}

private struct RowDivider: View {
    var body: some View {
        Divider()
            .background(CleansiaColors.outline.opacity(0.5))
            .padding(.horizontal, Spacing.m)
    }
}

private struct LogoutRow: View {
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                Image(systemName: "rectangle.portrait.and.arrow.right")
                    .font(.system(size: 18))
                    .foregroundColor(CleansiaColors.error)
                Text(L10n.Profile.logout)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.error)
                Spacer()
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity)
            .background(CleansiaColors.errorContainer.opacity(0.4))
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
        .buttonStyle(.plain)
    }
}

private extension String? {
    var nonBlankOrNil: String? {
        guard let value = self, !value.isBlank else { return nil }
        return value
    }
}

#if DEBUG
    struct ProfileHubContent_Previews: PreviewProvider {
        static var previews: some View {
            ProfileHubContent(
                data: ProfileData(
                    employee: EmployeeItem(
                        email: "jana@example.com",
                        firstName: "Jana",
                        lastName: "Nováková",
                        street: "Vinohradská 12",
                        city: "Praha",
                        zipCode: "120 00",
                        iban: "CZ6508000000192000145399"
                    ),
                    contractStatus: .approved
                ),
                onOpen: { _ in },
                onLogout: {}
            )
            .background(CleansiaColors.background)
        }
    }
#endif
