import CleansiaCore
import SwiftUI

/// The dashboard shortcuts row — the partner Android app's sixth dashboard section, ported.
///
/// **Four across, matching Android.** The Android version overflows in every non-English locale: its
/// label budget is ~70pt and "Pay history" runs 82pt in Czech, 83 in Slovak and Ukrainian, 95 in
/// Russian, so one tile wrapped to two lines while its neighbours stayed on one and the row went
/// ragged. Both platforms now hold the tiles to a common height and let the label shrink then
/// truncate, so a long translation costs legibility rather than layout.
///
/// The icons are SF Symbols chosen to read as the Material ones Android uses; the labels are the same
/// resource strings.
struct ShortcutsSection: View {
    let onProfile: () -> Void
    let onPayHistory: () -> Void
    let onDocuments: () -> Void
    let onHelp: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Shortcuts.sectionTitle)
                .cleansiaFont(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .padding(.leading, Spacing.xs)

            HStack(alignment: .top, spacing: Spacing.s) {
                ShortcutTile(icon: "person", label: L10n.Shortcuts.profile, action: onProfile)
                ShortcutTile(icon: "clock.arrow.circlepath", label: L10n.Shortcuts.payHistory, action: onPayHistory)
                ShortcutTile(icon: "doc.text", label: L10n.Shortcuts.documents, action: onDocuments)
                ShortcutTile(icon: "questionmark.circle", label: L10n.Shortcuts.help, action: onHelp)
            }
            // The tiles size themselves from the tallest label, so a two-line translation lifts all
            // four together instead of leaving one standing proud.
            .fixedSize(horizontal: false, vertical: true)
        }
        .padding(.horizontal, Spacing.m)
    }
}

private struct ShortcutTile: View {
    let icon: String
    let label: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(spacing: Spacing.xs) {
                ZStack {
                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                        .fill(CleansiaColors.primary.opacity(0.12))
                        .frame(width: 40, height: 40)
                    Image(systemName: icon)
                        .font(.system(size: 20, weight: .regular))
                        .foregroundColor(CleansiaColors.primary)
                }
                Text(label)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .multilineTextAlignment(.center)
                    // Two lines then ellipsis, and allowed to shrink first: "Історія виплат" is two
                    // words so it wraps, but Russian "История выплат" needs the scale factor as well.
                    .lineLimit(2)
                    .minimumScaleFactor(0.8)
                    .frame(maxWidth: .infinity)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
            .padding(.vertical, Spacing.m)
            .padding(.horizontal, Spacing.xs)
            .background(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .fill(CleansiaColors.surface)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
        .accessibilityLabel(label)
    }
}
