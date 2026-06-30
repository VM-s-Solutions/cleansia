import CleansiaCore
import SwiftUI

struct ProfileTab: View {
    @ObservedObject var profileVM: ProfileViewModel
    @ObservedObject var membershipVM: MembershipViewModel
    @ObservedObject var preferences: CustomerPreferencesModel
    let onOpen: (ProfileRoute) -> Void
    let onSignOut: () -> Void

    /// The membership card's "Subscribe to Plus" CTA routes to the paid
    /// subscribe flow — NOT Edit Profile. Held as a value so it's guarded by a
    /// test (a money surface that must not silently regress its destination).
    static let subscribeRoute: ProfileRoute = .subscribePlus

    @State private var showSignOutDialog = false

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(spacing: Spacing.l) {
                    ProfileHero(user: profileVM.currentUser, onEdit: { onOpen(.editProfile) })

                    MembershipManagementCard(vm: membershipVM, onSubscribeClick: { onOpen(Self.subscribeRoute) })
                        .padding(.horizontal, Spacing.m)

                    sectionGroup(title: L10n.Profile.groupAccount, rows: accountRows)
                    sectionGroup(title: L10n.Profile.groupPreferences, rows: preferenceRows)
                    sectionGroup(title: L10n.Profile.groupSupport, rows: supportRows)

                    DeleteAccountRow(onTap: { onOpen(.deleteAccount) })
                        .padding(.horizontal, Spacing.m)

                    CleansiaOutlinedButton(L10n.Profile.signOut, size: .medium) {
                        showSignOutDialog = true
                    }
                    .padding(.horizontal, Spacing.m)
                    .padding(.bottom, Spacing.xxl)
                }
                .padding(.top, Spacing.m)
            }
        }
        .navigationTitle(L10n.Shell.profile)
        .navigationBarTitleDisplayMode(.inline)
        .overlay { signOutOverlay }
    }

    private var accountRows: [ProfileRowItem] {
        [
            ProfileRowItem(icon: "person.crop.circle", label: L10n.Profile.rowEditProfile, route: .editProfile),
            ProfileRowItem(icon: "mappin.and.ellipse", label: L10n.AddressManager.profileRow, route: .addresses),
            ProfileRowItem(icon: "exclamationmark.bubble", label: L10n.Profile.rowDisputes, route: .disputes)
        ]
    }

    private var preferenceRows: [ProfileRowItem] {
        [
            ProfileRowItem(icon: "bell", label: L10n.Profile.rowNotifications, route: .notifications),
            ProfileRowItem(
                icon: "globe",
                label: L10n.Profile.rowLanguage,
                value: CustomerPreferencesLabels.languageSummary(
                    isFollowingSystem: preferences.isFollowingSystemLanguage,
                    tag: preferences.languageTag
                ),
                route: .language
            ),
            ProfileRowItem(
                icon: "moon",
                label: L10n.Profile.rowAppearance,
                value: CustomerPreferencesLabels.themeLabel(preferences.theme),
                route: .appearance
            ),
            ProfileRowItem(icon: "lock", label: L10n.Profile.rowSecurity, route: .security),
            ProfileRowItem(icon: "laptopcomputer.and.iphone", label: L10n.Profile.rowDevices, route: .devices)
        ]
    }

    private var supportRows: [ProfileRowItem] {
        [ProfileRowItem(icon: "questionmark.circle", label: L10n.Profile.rowHelp, route: .help)]
    }

    private func sectionGroup(title: String, rows: [ProfileRowItem]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(title.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.horizontal, Spacing.m)
            VStack(spacing: 0) {
                ForEach(rows.indices, id: \.self) { index in
                    ProfileRow(item: rows[index], onTap: { onOpen(rows[index].route) })
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

    @ViewBuilder
    private var signOutOverlay: some View {
        if showSignOutDialog {
            CleansiaDialog(
                title: L10n.Profile.signOutDialogTitle,
                confirmLabel: L10n.Profile.signOutDialogConfirm,
                onConfirm: {
                    showSignOutDialog = false
                    onSignOut()
                },
                onDismiss: { showSignOutDialog = false },
                message: L10n.Profile.signOutDialogMessage,
                dismissLabel: L10n.cancel,
                icon: "rectangle.portrait.and.arrow.right",
                destructive: true
            )
        }
    }
}

struct ProfileRowItem {
    let icon: String
    let label: String
    var value: String?
    let route: ProfileRoute

    init(icon: String, label: String, value: String? = nil, route: ProfileRoute) {
        self.icon = icon
        self.label = label
        self.value = value
        self.route = route
    }
}

private struct ProfileRow: View {
    let item: ProfileRowItem
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                Image(systemName: item.icon)
                    .foregroundColor(CleansiaColors.primary)
                    .frame(width: 24)
                Text(item.label)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                if let value = item.value {
                    Text(value)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Image(systemName: "chevron.right")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.m)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

private struct DeleteAccountRow: View {
    let onTap: () -> Void

    var body: some View {
        Button(role: .destructive, action: onTap) {
            Text(L10n.Profile.deleteAccount)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.error)
                .frame(maxWidth: .infinity)
                .padding(Spacing.m)
        }
    }
}

private struct ProfileHero: View {
    let user: CurrentUserProfile?
    let onEdit: () -> Void

    private var initials: String {
        let first = user?.firstName.first.map(String.init) ?? ""
        let last = user?.lastName.first.map(String.init) ?? ""
        return (first + last).uppercased()
    }

    var body: some View {
        VStack(spacing: Spacing.s) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.primaryContainer)
                    .frame(width: 88, height: 88)
                Text(initials)
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onPrimaryContainer)
            }
            Text(user?.fullName ?? "")
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onSurface)
            if let email = user?.email, !email.isEmpty {
                Text(email)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            CleansiaOutlinedButton(L10n.Profile.rowEditProfile, size: .small, action: onEdit)
                .fixedSize()
                .padding(.top, Spacing.xs)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.l)
    }
}

#if DEBUG
    struct ProfileHero_Previews: PreviewProvider {
        static var previews: some View {
            ProfileHero(
                user: CurrentUserProfile(
                    email: "jane@example.com",
                    firstName: "Jane",
                    lastName: "Doe",
                    phoneNumber: "+420123",
                    birthDate: nil,
                    preferredLanguageCode: "en",
                    isEmailConfirmed: true
                ),
                onEdit: {}
            )
            .background(CleansiaColors.background)
        }
    }
#endif
