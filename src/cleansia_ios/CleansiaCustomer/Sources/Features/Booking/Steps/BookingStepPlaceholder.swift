import CleansiaCore
import SwiftUI

struct BookingStepPlaceholder: View {
    let systemImage: String
    let title: String

    var body: some View {
        VStack(spacing: Spacing.m) {
            Spacer()
            Image(systemName: systemImage)
                .font(.system(size: 44))
                .foregroundColor(CleansiaColors.primary)
            Text(title)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
            Text(L10n.Booking.stepComingSoon)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.horizontal, Spacing.l)
    }
}

struct ServicesStep: View {
    var body: some View {
        BookingStepPlaceholder(systemImage: "sparkles", title: L10n.Booking.stepTitle(1))
    }
}

struct WhenWhereStep: View {
    var body: some View {
        BookingStepPlaceholder(systemImage: "calendar", title: L10n.Booking.stepTitle(2))
    }
}

struct ConfirmStep: View {
    var body: some View {
        BookingStepPlaceholder(systemImage: "checkmark.seal", title: L10n.Booking.stepTitle(3))
    }
}
