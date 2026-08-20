import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct ProfileHubContent: View {
    let data: ProfileData
    @ObservedObject var avatar: ProfileAvatarViewModel
    let avatarCache: RemoteImageCache
    let languageSummary: String
    let themeSummary: String
    let onOpen: (ProfileRoute) -> Void
    let onLogout: () -> Void

    // Opens the legal pages in Safari, which is the same destination the register screen's consent
    // sentence reaches via its AttributedString link — no SFSafariViewController exists anywhere in
    // this tree, and introducing one from the profile hub is not the place to start.
    @Environment(\.openURL) private var openURL

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
                            topInset: proxy.safeAreaInsets.top,
                            display: avatar.display,
                            avatarCache: avatarCache,
                            onAvatarLoadFailure: { photo in
                                Task { await avatar.loadFailed(fileName: photo.fileName) }
                            },
                            onAvatarLoadSuccess: avatar.loadSucceeded
                        )
                        sectionGroup(title: L10n.Profile.groupAccount, rows: accountRows)
                        sectionGroup(title: L10n.Profile.groupWorkLegal, rows: workLegalRows)
                        sectionGroup(title: L10n.Profile.groupPreferences, rows: preferenceRows)
                        sectionGroup(title: L10n.Profile.groupLegal, rows: legalRows)
                        // Out of the preferences group and onto its own card beside logout: the two
                        // account-ending actions belong together, and grouping deletion with
                        // "language / theme" read as a preference.
                        VStack(spacing: Spacing.m) {
                            DeleteAccountRow(onTap: { onOpen(.deleteAccount) })
                            LogoutRow(onTap: onLogout)
                        }
                        // One inset for both, matching sectionGroup's — previously each carried its
                        // own literal, which is how "aligned" survives only until someone edits one.
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
                action: .route(.personal(onboarding: false))
            ),
            ProfileHubRowItem(
                icon: "mappin.and.ellipse",
                title: L10n.Profile.address,
                summary: displayAddress,
                action: .route(.address(onboarding: false))
            ),
            ProfileHubRowItem(
                icon: "phone",
                title: L10n.Profile.emergencyContact,
                summary: displayEmergency,
                action: .route(.emergency)
            )
        ]
    }

    private var workLegalRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "person.text.rectangle",
                title: L10n.Profile.identification,
                summary: employee.passportId.nonBlankOrNil ?? L10n.Profile.noData,
                action: .route(.identification(onboarding: false))
            ),
            ProfileHubRowItem(
                icon: "building.columns",
                title: L10n.Profile.bankDetails,
                summary: data.payoutSummary.nonBlankOrNil ?? L10n.Profile.noData,
                action: .route(.bank(onboarding: false))
            ),
            ProfileHubRowItem(
                icon: "doc.text",
                title: L10n.Profile.myDocuments,
                summary: L10n.Profile.documentsSummary,
                action: .route(.documents)
            )
        ]
    }

    private var preferenceRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "location.circle",
                title: L10n.JobRadius.title,
                summary: JobRadiusSelection(radiusKm: data.jobRadiusKm).summary,
                action: .route(.jobRadius)
            ),
            ProfileHubRowItem(icon: "globe", title: L10n.Profile.language, summary: languageSummary, action: .route(.language)),
            ProfileHubRowItem(icon: "moon", title: L10n.Profile.theme, summary: themeSummary, action: .route(.theme)),
            ProfileHubRowItem(
                icon: "laptopcomputer.and.iphone",
                title: L10n.Devices.title,
                summary: L10n.Profile.devicesSummary,
                action: .route(.devices)
            )
        ]
    }

    /// A signed-in cleaner previously had no route to the privacy policy: the only link lived in
    /// the register screen's consent sentence, which they see once and never again. Both stores
    /// expect it reachable from inside the app.
    ///
    /// URLs come from `CleansiaWeb` rather than being spelled out here — that file's own doc
    /// comment forbids any other source spelling the domain.
    private var legalRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "doc.plaintext",
                title: L10n.Profile.terms,
                summary: L10n.Profile.termsSummary,
                action: .openURL(CleansiaWeb.termsURL)
            ),
            ProfileHubRowItem(
                icon: "hand.raised",
                title: L10n.Profile.privacy,
                summary: L10n.Profile.privacySummary,
                action: .openURL(CleansiaWeb.privacyURL)
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
                    ProfileSectionRow(item: rows[index], onTap: {
                        switch rows[index].action {
                        case let .route(route): onOpen(route)
                        case let .openURL(url): openURL(url)
                        }
                    })
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
    /// What tapping the row does.
    ///
    /// Deliberately an action rather than the `ProfileRoute` this used to be. The legal rows open a
    /// web page; they are not destinations in the profile navigation stack, and giving them routes
    /// would mean new `ProfileRoute` cases — which every exhaustive switch over that enum has to
    /// answer for, including one in `RegistrationLockView` that has no `default` and broke the
    /// build the last time a case was added. A row that opens a URL should not be able to do that.
    let action: Action

    enum Action {
        case route(ProfileRoute)
        case openURL(URL)
    }
}

private struct ProfileHero: View {
    let employee: EmployeeItem
    let contractStatus: ContractStatus?
    var topInset: CGFloat = 0
    let display: AvatarDisplay
    let avatarCache: RemoteImageCache
    let onAvatarLoadFailure: (ProfilePhoto) -> Void
    let onAvatarLoadSuccess: () -> Void

    var body: some View {
        HStack(spacing: 14) {
            portrait
            VStack(alignment: .leading, spacing: 2) {
                Text(name)
                    .cleansiaFont(CleansiaTypography.headlineSmall)
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

    /// A portrait, not a control: the photo is changed on the Personal data section, where the rest of
    /// the cleaner's own details are edited, so nothing here is tappable and there is no camera badge to
    /// promise otherwise. Hidden from VoiceOver because the name it illustrates is read out beside it.
    ///
    /// The load callbacks stay: this is still a rendered remote image, and its signed URL still expires.
    private var portrait: some View {
        ProfileAvatar(
            display: display,
            initials: initials,
            cache: avatarCache,
            onLoadFailure: onAvatarLoadFailure,
            onLoadSuccess: onAvatarLoadSuccess
        )
        .accessibilityHidden(true)
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

/// The account-deletion request, styled to match the customer app's equivalent: an error-tinted
/// icon and error-coloured label on a surface card.
///
/// Red despite filing a request rather than deleting anything today — the colour marks where the
/// account-ending actions live, and it is what the customer app already taught users to look for.
private struct DeleteAccountRow: View {
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.error.opacity(0.12))
                        .frame(width: 32, height: 32)
                    Image(systemName: "trash")
                        .font(.system(size: 16))
                        .foregroundColor(CleansiaColors.error)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(L10n.DeleteAccount.rowTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.error)
                    Text(L10n.DeleteAccount.rowSummary)
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
            .frame(maxWidth: .infinity)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
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
                        zipCode: "120 00"
                    ),
                    contractStatus: .approved,
                    payoutSummary: "19-2000145399/0800"
                ),
                avatar: ProfileAvatarViewModel(client: LivePartnerUserClient(), snackbar: SnackbarController()),
                avatarCache: RemoteImageCache(),
                languageSummary: "Čeština",
                themeSummary: "Follow system",
                onOpen: { _ in },
                onLogout: {}
            )
        }
    }
#endif
