import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct ProfileHubContent: View {
    let data: ProfileData
    let languageSummary: String
    let themeSummary: String
    let onOpen: (ProfileRoute) -> Void
    let onLogout: () -> Void

    private var employee: EmployeeItem {
        data.employee
    }

    var body: some View {
        GeometryReader { proxy in
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                ScrollView {
                    VStack(spacing: Spacing.l) {
                        ProfileHero(
                            employee: employee,
                            contractStatus: data.contractStatus,
                            topInset: proxy.safeAreaInsets.top
                        )
                        sectionGroup(title: L10n.Profile.groupAccount, rows: accountRows)
                        sectionGroup(title: L10n.Profile.groupWorkLegal, rows: workLegalRows)
                        sectionGroup(title: L10n.Profile.groupPreferences, rows: preferenceRows)
                        LogoutRow(onTap: onLogout)
                            .padding(.horizontal, Spacing.m)
                            .padding(.bottom, Spacing.xxl)
                    }
                }
                .ignoresSafeArea(.container, edges: .top)
            }
        }
    }

    private var accountRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "person",
                title: L10n.Profile.personal,
                summary: displayName,
                route: .personal(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "mappin.and.ellipse",
                title: L10n.Profile.address,
                summary: displayAddress,
                route: .address(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "phone",
                title: L10n.Profile.emergencyContact,
                summary: displayEmergency,
                route: .emergency
            )
        ]
    }

    private var workLegalRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "person.text.rectangle",
                title: L10n.Profile.identification,
                summary: employee.passportId.nonBlankOrNil ?? L10n.Profile.noData,
                route: .identification(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "building.columns",
                title: L10n.Profile.bankDetails,
                summary: employee.iban.nonBlankOrNil ?? L10n.Profile.noData,
                route: .bank(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "doc.text",
                title: L10n.Profile.myDocuments,
                summary: L10n.Profile.documentsSummary,
                route: .documents
            )
        ]
    }

    private var preferenceRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(icon: "globe", title: L10n.Profile.language, summary: languageSummary, route: .language),
            ProfileHubRowItem(icon: "moon", title: L10n.Profile.theme, summary: themeSummary, route: .theme),
            ProfileHubRowItem(
                icon: "laptopcomputer.and.iphone",
                title: L10n.Devices.title,
                summary: L10n.Profile.devicesSummary,
                route: .devices
            )
        ]
    }

    private func sectionGroup(title: String, rows: [ProfileHubRowItem]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(title.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.horizontal, Spacing.m)
            VStack(spacing: 0) {
                ForEach(rows.indices, id: \.self) { index in
                    ProfileSectionRow(item: rows[index], onTap: { onOpen(rows[index].route) })
                    if index < rows.count - 1 {
                        Divider().padding(.leading, Spacing.xl)
                    }
                }
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
            .padding(.horizontal, Spacing.m)
        }
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

private struct ProfileHubRowItem {
    let icon: String
    let title: String
    let summary: String
    let route: ProfileRoute
}

private struct ProfileHero: View {
    let employee: EmployeeItem
    let contractStatus: ContractStatus?
    var topInset: CGFloat = 0

    var body: some View {
        HStack(spacing: 14) {
            ZStack {
                Circle()
                    .fill(Color.white)
                    .overlay(Circle().stroke(Color.white.opacity(0.35), lineWidth: 3))
                    .frame(width: 72, height: 72)
                Text(initials)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.primary)
            }
            VStack(alignment: .leading, spacing: 2) {
                Text(name)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(.white)
                    .lineLimit(1)
                if let email = employee.email.nonBlankOrNil {
                    Text(email)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(.white.opacity(0.85))
                        .lineLimit(1)
                }
                if let contractStatus {
                    ContractStatusChip(status: contractStatus)
                        .padding(.top, Spacing.xxs)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.top, Spacing.m + topInset)
        .padding(.bottom, Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            LinearGradient(colors: BrandGradient.blue.colors, startPoint: .top, endPoint: .bottom)
        )
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

/// Unlike the customer TierBadge (white translucent capsule), the chip keeps
/// Android's semantic palette — color encodes the contract state.
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

private struct ProfileSectionRow: View {
    let item: ProfileHubRowItem
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.12))
                        .frame(width: 32, height: 32)
                    Image(systemName: item.icon)
                        .font(.system(size: 16))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(item.title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(item.summary)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s + 2)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

private struct LogoutRow: View {
    let onTap: () -> Void

    var body: some View {
        Button(role: .destructive, action: onTap) {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "rectangle.portrait.and.arrow.right")
                    .font(.system(size: 16, weight: .semibold))
                Text(L10n.Profile.logout)
                    .font(CleansiaTypography.labelLarge)
            }
            .foregroundColor(CleansiaColors.error)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(CleansiaColors.error.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.large))
            .overlay {
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.error.opacity(0.4), lineWidth: 1)
            }
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
                languageSummary: "Čeština",
                themeSummary: "Follow system",
                onOpen: { _ in },
                onLogout: {}
            )
        }
    }
#endif
