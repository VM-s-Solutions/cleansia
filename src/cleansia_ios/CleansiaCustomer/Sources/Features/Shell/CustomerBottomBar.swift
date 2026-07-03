import CleansiaCore
import SwiftUI

/// The floating pill bar + center Book FAB composite — the `CustomBottomBar`
/// port (`MainShell.kt:363-407`): 64pt pill, 16pt side margins, radius 32,
/// surface fill + outline-variant stroke, a reserved 72pt center gap, the FAB
/// half-overlapping the pill top. Mounted via `.safeAreaInset(edge: .bottom)`
/// so every tab's scroll content clears the full 88pt composite (ADR-0022 D3).
struct CustomerBottomBar: View {
    let selection: CustomerShellTab
    let onSelect: (CustomerShellTab) -> Void
    let onBook: () -> Void

    var body: some View {
        pill
            .overlay(alignment: .top) {
                BookFab(action: onBook)
                    .offset(y: -12)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
    }

    private var pill: some View {
        HStack(spacing: 0) {
            NavSlot(tab: .home, selection: selection, onSelect: onSelect)
            NavSlot(tab: .orders, selection: selection, onSelect: onSelect)
            Color.clear.frame(width: 72)
            NavSlot(tab: .rewards, selection: selection, onSelect: onSelect)
            NavSlot(tab: .profile, selection: selection, onSelect: onSelect)
        }
        .padding(.horizontal, 8)
        .frame(height: 64)
        .frame(maxWidth: .infinity)
        .background(RoundedRectangle(cornerRadius: 32).fill(CleansiaColors.surface))
        .overlay(RoundedRectangle(cornerRadius: 32).stroke(CleansiaColors.outlineVariant, lineWidth: 1))
    }
}

private struct NavSlot: View {
    let tab: CustomerShellTab
    let selection: CustomerShellTab
    let onSelect: (CustomerShellTab) -> Void

    private var isSelected: Bool {
        tab == selection
    }

    var body: some View {
        Button {
            onSelect(tab)
        } label: {
            VStack(spacing: 0) {
                Image(systemName: tab.systemImage)
                    .font(.system(size: 20))
                    .frame(width: 24, height: 24)
                Spacer().frame(height: 2)
                Text(tab.label)
                    .font(isSelected ? CleansiaTypography.labelSmall : CleansiaTypography.labelMedium)
                Spacer().frame(height: 3)
                Capsule()
                    .fill(CleansiaColors.primary)
                    .frame(width: isSelected ? 20 : 0, height: 3)
                    .animation(.easeInOut(duration: 0.2), value: isSelected)
            }
            .foregroundColor(isSelected ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant)
            .frame(maxWidth: .infinity)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(Text(verbatim: tab.label))
        .accessibilityAddTraits(isSelected ? [.isSelected] : [])
    }
}

private struct BookFab: View {
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            // Android's FAB glyph is CleaningServices (the broom); SF has no broom,
            // so this follows the T-0372 category ruling's nearest-meaning pick.
            Image(systemName: "bubbles.and.sparkles")
                .font(.system(size: 34, weight: .semibold))
                .foregroundColor(CleansiaColors.onPrimary)
                .frame(width: 74, height: 74)
                .background(Circle().fill(CleansiaColors.primary))
                .overlay(Circle().stroke(CleansiaColors.background, lineWidth: 4))
        }
        .accessibilityLabel(Text(verbatim: L10n.Shell.book))
    }
}

#if DEBUG
    struct CustomerBottomBar_Previews: PreviewProvider {
        static var previews: some View {
            VStack {
                Spacer()
                CustomerBottomBar(selection: .home, onSelect: { _ in }, onBook: {})
            }
            .background(CleansiaColors.background)
        }
    }
#endif
