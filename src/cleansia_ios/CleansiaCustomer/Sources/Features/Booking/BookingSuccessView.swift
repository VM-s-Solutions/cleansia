import CleansiaCore
import SwiftUI

struct BookingSuccessView: View {
    let confirmationCode: String
    let onDone: () -> Void

    var body: some View {
        VStack(spacing: Spacing.l) {
            Spacer()
            ZStack {
                Circle()
                    .fill(CleansiaColors.successText.opacity(0.15))
                    .frame(width: 96, height: 96)
                Image(systemName: "checkmark")
                    .font(.system(size: 44, weight: .bold))
                    .foregroundColor(CleansiaColors.successText)
            }
            VStack(spacing: Spacing.s) {
                Text(L10n.Booking.successTitle)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onBackground)
                    .multilineTextAlignment(.center)
                Text(L10n.Booking.successSubtitle)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
            }
            if !confirmationCode.isBlank {
                confirmationCard
            }
            Spacer()
            CleansiaPrimaryButton(L10n.Booking.successGoHome, action: onDone)
        }
        .padding(Spacing.l)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }

    private var confirmationCard: some View {
        VStack(spacing: Spacing.xxs) {
            Text(L10n.Booking.successConfirmationCode)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(confirmationCode)
                .font(CleansiaTypography.titleLarge)
                .fontWeight(.bold)
                .foregroundColor(CleansiaColors.primary)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }
}

#if DEBUG
    struct BookingSuccessView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSuccessView(confirmationCode: "CLN-12345", onDone: {})
        }
    }
#endif
