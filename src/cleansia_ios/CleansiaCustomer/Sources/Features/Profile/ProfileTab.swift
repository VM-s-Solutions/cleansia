import CleansiaCore
import SwiftUI

struct ProfileTab: View {
    @ObservedObject var profileVM: ProfileViewModel
    @ObservedObject var membershipVM: MembershipViewModel
    @ObservedObject var preferences: CustomerPreferencesModel
    let onOpen: (ShellRoute) -> Void
    let onSignOut: () -> Void

    /// The membership card's "Subscribe to Plus" CTA routes to the paid
    /// subscribe flow — NOT Edit Profile. Held as a value so it's guarded by a
    /// test (a money surface that must not silently regress its destination).
    static let subscribeRoute: ShellRoute = .subscribePlus

    @State private var showSignOutDialog = false

    private var tierLabel: String {
        membershipVM.current?.hasMembership == true ? L10n.Profile.tierPlus : L10n.Profile.tierRegular
    }

    var body: some View {
        GeometryReader { proxy in
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                ScrollView {
                    VStack(spacing: Spacing.l) {
                        ProfileHeader(
                            user: profileVM.currentUser,
                            tier: tierLabel,
                            topInset: proxy.safeAreaInsets.top,
                            onEdit: { onOpen(.editProfile(showBookingHint: false)) }
                        )

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
                }
                .ignoresSafeArea(.container, edges: .top)
            }
        }
        .overlay { signOutOverlay }
    }

    private var accountRows: [ProfileRowItem] {
        [
            ProfileRowItem(
                icon: "person.crop.circle",
                label: L10n.Profile.rowEditProfile,
                route: .editProfile(showBookingHint: false)
            ),
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
    let route: ShellRoute

    init(icon: String, label: String, value: String? = nil, route: ShellRoute) {
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
            HStack(spacing: Spacing.xs) {
                Image(systemName: "trash")
                    .font(.system(size: 16, weight: .semibold))
                Text(L10n.Profile.deleteAccount)
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

private struct ProfileHeader: View {
    let user: CurrentUserProfile?
    let tier: String
    var topInset: CGFloat = 0
    let onEdit: () -> Void

    var body: some View {
        // The stats card (bookings / saved / member-since) is intentionally hidden:
        // no per-user source exists on the mobile contract, so any values would be
        // fabricated. T-0392 wires the real stats and restores the card here.
        HeroGradient(user: user, tier: tier, topInset: topInset, onEdit: onEdit)
    }
}

private struct HeroGradient: View {
    let user: CurrentUserProfile?
    let tier: String
    var topInset: CGFloat = 0
    let onEdit: () -> Void

    private var initials: String {
        let first = user?.firstName.first.map(String.init) ?? ""
        let last = user?.lastName.first.map(String.init) ?? ""
        return (first + last).uppercased()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
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
                    Text(user?.fullName ?? "")
                        .font(CleansiaTypography.headlineSmall)
                        .foregroundColor(.white)
                        .lineLimit(1)
                    if let email = user?.email, !email.isEmpty {
                        Text(email)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(.white.opacity(0.85))
                            .lineLimit(1)
                    }
                    TierBadge(tier: tier)
                        .padding(.top, Spacing.xxs)
                }
                Spacer(minLength: 0)
            }
            HStack {
                Spacer()
                EditProfileChip(onEdit: onEdit)
            }
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.top, Spacing.m + topInset)
        .padding(.bottom, Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            LinearGradient(colors: BrandGradient.blue.colors, startPoint: .top, endPoint: .bottom)
        )
    }
}

private struct TierBadge: View {
    let tier: String

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: "crown.fill")
                .font(.system(size: 10))
            Text(tier)
                .font(CleansiaTypography.labelSmall)
        }
        .foregroundColor(.white)
        .padding(.horizontal, Spacing.xs)
        .padding(.vertical, 3)
        .background(Color.white.opacity(0.22), in: Capsule())
    }
}

private struct EditProfileChip: View {
    let onEdit: () -> Void

    var body: some View {
        Button(action: onEdit) {
            HStack(spacing: Spacing.xxs) {
                Image(systemName: "pencil")
                    .font(.system(size: 12, weight: .semibold))
                Text(L10n.Profile.rowEditProfile)
                    .font(CleansiaTypography.labelLarge)
            }
            .foregroundColor(.white)
            .padding(.horizontal, 14)
            .padding(.vertical, Spacing.xs)
            .background(Color.white.opacity(0.22), in: Capsule())
        }
        .buttonStyle(.plain)
    }
}

#if DEBUG
    struct ProfileHeader_Previews: PreviewProvider {
        static var previews: some View {
            ProfileHeader(
                user: CurrentUserProfile(
                    id: "user-1",
                    email: "jane@example.com",
                    firstName: "Jane",
                    lastName: "Doe",
                    phoneNumber: "+420123",
                    birthDate: nil,
                    preferredLanguageCode: "en",
                    isEmailConfirmed: true
                ),
                tier: "Regular",
                onEdit: {}
            )
            .background(CleansiaColors.background)
        }
    }
#endif
