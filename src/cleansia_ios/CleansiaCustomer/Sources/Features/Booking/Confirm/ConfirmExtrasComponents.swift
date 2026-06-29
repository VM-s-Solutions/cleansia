import CleansiaCore
import SwiftUI

struct SpecialInstructionsField: View {
    @Binding var text: String

    var body: some View {
        ZStack(alignment: .topLeading) {
            if text.isEmpty {
                Text(L10n.Booking.specialInstructionsHint)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.horizontal, Spacing.s + 4)
                    .padding(.vertical, Spacing.s + 4)
            }
            TextEditor(text: $text)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .scrollContentBackground(.hidden)
                .padding(Spacing.xs)
                .frame(minHeight: 96)
        }
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outline, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

struct PreferredCleanerPicker: View {
    @ObservedObject var viewModel: PreferredCleanerViewModel
    let selectedId: String?
    let onSelect: (String?) -> Void

    @State private var showDialog = false

    private var selected: ServingCleaner? {
        viewModel.cleaners.first { $0.id == selectedId }
    }

    var body: some View {
        Group {
            if viewModel.isVisible {
                pickerRow
            }
        }
        .task { await viewModel.load() }
    }

    private var pickerRow: some View {
        HStack(spacing: Spacing.s) {
            Button {
                showDialog = true
            } label: {
                HStack(spacing: Spacing.s) {
                    ZStack {
                        Circle()
                            .fill(CleansiaColors.primary.opacity(0.15))
                            .frame(width: 36, height: 36)
                        Image(systemName: "person")
                            .font(.system(size: 18))
                            .foregroundColor(CleansiaColors.primary)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text(L10n.Booking.preferredCleanerTitle)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        Text(selected?.fullName ?? L10n.Booking.preferredCleanerSubtitle)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(selected == nil ? CleansiaColors.onSurfaceVariant : CleansiaColors.primary)
                    }
                    Spacer()
                    if selected == nil {
                        Image(systemName: "chevron.right")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
            }
            .buttonStyle(.plain)
            if selected != nil {
                Button {
                    onSelect(nil)
                } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .accessibilityLabel(L10n.Booking.preferredCleanerClear)
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        .sheet(isPresented: $showDialog) {
            PreferredCleanerSheet(
                cleaners: viewModel.cleaners,
                selectedId: selectedId,
                onSelect: { id in
                    onSelect(id)
                    showDialog = false
                },
                onDismiss: { showDialog = false }
            )
        }
    }
}

private struct PreferredCleanerSheet: View {
    let cleaners: [ServingCleaner]
    let selectedId: String?
    let onSelect: (String) -> Void
    let onDismiss: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: Spacing.s) {
                    ForEach(cleaners) { cleaner in
                        CleanerRow(
                            cleaner: cleaner,
                            selected: cleaner.id == selectedId,
                            action: { onSelect(cleaner.id) }
                        )
                    }
                }
                .padding(Spacing.l)
            }
            .background(CleansiaColors.background.ignoresSafeArea())
            .navigationTitle(L10n.Booking.preferredCleanerDialogTitle)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(L10n.Booking.back, action: onDismiss)
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}

private struct CleanerRow: View {
    let cleaner: ServingCleaner
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.20))
                        .frame(width: 36, height: 36)
                    Text(cleaner.fullName.prefix(1).uppercased())
                        .font(CleansiaTypography.titleMedium)
                        .fontWeight(.bold)
                        .foregroundColor(CleansiaColors.primary)
                }
                Text(cleaner.fullName)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
            }
            .padding(.horizontal, Spacing.s)
            .padding(.vertical, Spacing.s)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
        }
        .buttonStyle(.plain)
    }
}
