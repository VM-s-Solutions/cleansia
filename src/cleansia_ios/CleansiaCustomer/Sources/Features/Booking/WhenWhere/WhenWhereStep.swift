import CleansiaCore
import SwiftUI

struct WhenWhereStep: View {
    @ObservedObject var viewModel: BookingViewModel
    @Environment(\.savedAddressRepository) private var savedAddressRepository
    let geocoding: GeocodingService
    let mapProvider: MapProvider

    @State private var showAddressChooser = false

    private let days = BookingTimeSlots.days()

    private var selectedDay: BookingDay? {
        days.first { BookingDateFormat.dayLabel($0.date) == viewModel.state.selectedDate }
    }

    private var visibleSlots: [BookingTimeSlot] {
        guard let selectedDay else { return [] }
        return BookingTimeSlots.slots(for: selectedDay.date).filter { $0.state != .unavailable }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                SectionLabel(L10n.Booking.whereLabel)
                    .padding(.horizontal, Spacing.l)
                SelectAddressRow(
                    street: viewModel.state.street,
                    city: viewModel.state.city,
                    action: { showAddressChooser = true }
                )
                .padding(.horizontal, Spacing.l)
                .padding(.top, Spacing.s)

                SectionLabel(L10n.Booking.whenLabel)
                    .padding(.horizontal, Spacing.l)
                    .padding(.top, Spacing.xl)
                dayStrip
                    .padding(.top, Spacing.s)

                timeHeader
                    .padding(.horizontal, Spacing.l)
                    .padding(.top, Spacing.l)
                timeSlots
                    .padding(.horizontal, Spacing.l)
                    .padding(.top, Spacing.s)

                cancelHint
                    .padding(.horizontal, Spacing.l)
                    .padding(.top, Spacing.m)
            }
            .padding(.vertical, Spacing.m)
        }
        .fullScreenCover(isPresented: $showAddressChooser) {
            BookingSavedAddressChooserView(
                repository: savedAddressRepository,
                currentSavedAddressId: viewModel.state.savedAddressId,
                geocoding: geocoding,
                mapProvider: mapProvider,
                onPickSaved: { address in
                    viewModel.update { BookingSavedAddressApply.applied($0, address: address) }
                    showAddressChooser = false
                },
                onPickNew: { address in
                    viewModel.applyAddress(address)
                    showAddressChooser = false
                },
                onDismiss: { showAddressChooser = false }
            )
            .environment(\.bookingAddressSaveOffered, true)
        }
        .onAppear(perform: pruneStaleTime)
        .onChange(of: viewModel.state.selectedDate) { _ in pruneStaleTime() }
    }

    private func pruneStaleTime() {
        guard let selectedDay else { return }
        viewModel.clearSelectedTimeIfUnavailable(slots: BookingTimeSlots.slots(for: selectedDay.date))
    }

    private var dayStrip: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: Spacing.s) {
                ForEach(days) { day in
                    DayChipView(
                        day: day,
                        selected: selectedDay?.date == day.date,
                        action: { viewModel.selectDay(day.date) }
                    )
                }
            }
            .padding(.horizontal, Spacing.l)
        }
    }

    private var timeHeader: some View {
        HStack {
            SectionLabel(L10n.Booking.selectTime)
            Spacer()
            Text(L10n.Booking.arrivalWindow)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    @ViewBuilder
    private var timeSlots: some View {
        if selectedDay == nil {
            EmptyView()
        } else if visibleSlots.isEmpty {
            Text(L10n.Booking.allSlotsBooked)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.vertical, Spacing.m)
        } else {
            VStack(spacing: Spacing.s) {
                ForEach(visibleSlots) { slot in
                    TimeSlotRow(
                        slot: slot,
                        selected: viewModel.state.selectedTime == slot.time,
                        action: {
                            if let date = selectedDay?.date {
                                viewModel.selectTime(slot.time, on: date)
                            }
                        }
                    )
                }
            }
        }
    }

    private var cancelHint: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "info.circle")
                .font(.system(size: 12))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Booking.cancelHint)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }
}

private struct SelectAddressRow: View {
    let street: String
    let city: String
    let action: () -> Void

    private var hasSelection: Bool {
        !street.isBlank
    }

    var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primaryContainer.opacity(0.6))
                        .frame(width: 40, height: 40)
                    Image(systemName: "mappin.and.ellipse")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    if hasSelection {
                        Text(street)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                            .lineLimit(1)
                        if !city.isBlank {
                            Text(city)
                                .font(CleansiaTypography.labelMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .lineLimit(1)
                        }
                    } else {
                        Text(L10n.Booking.selectAddress)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        Text(L10n.Booking.selectAddressHint)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.m)
            .background(hasSelection ? CleansiaColors.primaryContainer.opacity(0.35) : CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        hasSelection ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: hasSelection ? 2 : 1
                    )
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        }
        .buttonStyle(.plain)
    }
}

private struct DayChipView: View {
    let day: BookingDay
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(spacing: 4) {
                Text(BookingDateFormat.dayLabel(day.date))
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(selected ? CleansiaColors.onPrimary : CleansiaColors.onSurfaceVariant)
                Text("\(day.dayNumber)")
                    .font(CleansiaTypography.titleMedium)
                    .fontWeight(.bold)
                    .foregroundColor(selected ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                Circle()
                    .fill(day.isToday && !selected ? CleansiaColors.primary : Color.clear)
                    .frame(width: 4, height: 4)
            }
            .frame(width: 52)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, Spacing.s)
            .background(selected ? CleansiaColors.primary : CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(selected ? CleansiaColors.primary : CleansiaColors.outlineVariant, lineWidth: 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        }
        .buttonStyle(.plain)
    }
}

private struct TimeSlotRow: View {
    let slot: BookingTimeSlot
    let selected: Bool
    let action: () -> Void

    private static let expressOrange = Color(red: 0.918, green: 0.345, blue: 0.047)

    private var isExpress: Bool {
        slot.state == .express
    }

    private var isEarliest: Bool {
        slot.state == .earliest
    }

    var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                if isExpress {
                    Image(systemName: "bolt.fill")
                        .font(.system(size: 16))
                        .foregroundColor(Self.expressOrange)
                } else if isEarliest {
                    Image(systemName: "clock")
                        .font(.system(size: 16))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(slot.time)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(selected ? CleansiaColors.primary : CleansiaColors.onSurface)
                    if isExpress {
                        Text(L10n.Booking.slotExpress)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(Self.expressOrange)
                    } else if isEarliest {
                        Text(L10n.Booking.slotEarliest)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.primary)
                    }
                }
                Spacer()
                if selected {
                    Image(systemName: "checkmark")
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundColor(CleansiaColors.primary)
                } else {
                    Text(L10n.Booking.slotSelect)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .leading) {
                if isExpress {
                    Rectangle()
                        .fill(Self.expressOrange)
                        .frame(width: 4)
                }
            }
            .background(CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        }
        .buttonStyle(.plain)
    }
}

private struct SectionLabel: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(text)
            .font(CleansiaTypography.titleMedium)
            .fontWeight(.semibold)
            .foregroundColor(CleansiaColors.onBackground)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

#if DEBUG
    struct WhenWhereStep_Previews: PreviewProvider {
        static var previews: some View {
            WhenWhereStep(
                viewModel: BookingViewModel(),
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider()
            )
            .background(CleansiaColors.background)
        }
    }
#endif
