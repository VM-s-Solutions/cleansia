import CleansiaCore
import SwiftUI

struct WhenWhereStep: View {
    @ObservedObject var viewModel: BookingViewModel
    @Environment(\.savedAddressRepository) private var savedAddressRepository
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    var serviceArea: ServiceAreaProvider?

    @State private var showAddressChooser = false

    private let days = BookingTimeSlots.days()

    private var selectedDay: BookingDay? {
        days.first { BookingDateFormat.dayLabel($0.date) == viewModel.state.selectedDate }
    }

    private var daySlots: [BookingTimeSlot] {
        guard let selectedDay else { return [] }
        return BookingTimeSlots.slots(for: selectedDay.date)
    }

    /// The waiver note belongs under a day that actually offers an express slot, and nowhere else.
    private var offersExpressSlot: Bool {
        daySlots.contains { $0.state == .express }
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

                if offersExpressSlot {
                    ExpressWaiverNote(
                        status: viewModel.expressWaiverStatus,
                        remaining: viewModel.expressUpgradesRemaining
                    )
                    .padding(.horizontal, Spacing.l)
                    .padding(.top, Spacing.s)
                }

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
                serviceArea: serviceArea,
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
        .task { await viewModel.loadMembership() }
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

    /// Nothing until a day is picked; then the part of day and that part's quarter hours.
    @ViewBuilder
    private var timeSlots: some View {
        if let selectedDay {
            let slots = daySlots
            if !slots.contains(where: { $0.state != .unavailable }) {
                Text(L10n.Booking.allSlotsBooked)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.vertical, Spacing.m)
            } else {
                DayPartTimePicker(
                    slots: slots,
                    selectedTime: viewModel.state.selectedTime,
                    resetKey: selectedDay.date,
                    expressWaived: viewModel.expressWaiverStatus == .available,
                    onSelect: { viewModel.selectTime($0, on: selectedDay.date) }
                )
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

private struct ExpressWaiverNote: View {
    let status: ExpressWaiverStatus
    let remaining: Int

    var body: some View {
        switch status {
        case .none:
            EmptyView()
        case .available:
            note(
                icon: "bolt.fill",
                text: L10n.Booking.expressWaiverAvailable(remaining),
                tint: CleansiaColors.primaryText
            )
        case .exhausted:
            note(icon: "info.circle", text: L10n.Booking.expressWaiverUsed, tint: CleansiaColors.onSurfaceVariant)
        }
    }

    private func note(icon: String, text: String, tint: Color) -> some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Image(systemName: icon)
                .font(.system(size: 12))
                .foregroundColor(tint)
            Text(text)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(tint)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
    }
}

/// Three part-of-day buttons, each with its first and last arrival, over a 4 × 4 grid of the chosen part's
/// slots — the web wizard's time step (→ /customer-app/ordering-flow#step-2-date-time) and Android's
/// `DayPartTimePicker`. Choosing a part never changes the booked time; it only changes which sixteen
/// slots are on screen. The step opens on the part holding the booked time, which carries a dot while
/// another part is browsed; a part with nothing bookable is disabled, and so is a slot inside the lead
/// time. Shared with the recurring schedule's time, whose slots are all available. `resetKey` drops the
/// part being browsed, e.g. a new day reopens on the part holding the booked time. Nothing animates.
struct DayPartTimePicker: View {
    let slots: [BookingTimeSlot]
    let selectedTime: String
    var resetKey: Date?
    var expressWaived = false
    let onSelect: (String) -> Void

    @State private var browsed: DayPart?

    private static let columns = 4

    private var parts: [DayPartSlots] {
        BookingTimeSlots.dayParts(slots)
    }

    private func active(in parts: [DayPartSlots]) -> DayPart {
        if let browsed, parts.contains(where: { $0.part == browsed && $0.isBookable }) { return browsed }
        return BookingTimeSlots.openingDayPart(parts, selectedTime: selectedTime)
    }

    private var expressLabel: String {
        expressWaived ? L10n.Booking.slotExpressWaived : L10n.Booking.slotExpress
    }

    var body: some View {
        let parts = parts
        let active = active(in: parts)
        let visible = parts.first { $0.part == active }?.slots ?? []
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(spacing: Spacing.xs) {
                ForEach(parts, id: \.part) { part in
                    DayPartButton(
                        part: part,
                        active: part.part == active,
                        holdsSelection: part.part == DayPart.of(selectedTime) && part.part != active,
                        action: { browsed = part.part }
                    )
                }
            }
            .fixedSize(horizontal: false, vertical: true)
            .padding(.bottom, Spacing.xxs)
            ForEach(Array(stride(from: 0, to: visible.count, by: Self.columns)), id: \.self) { start in
                HStack(spacing: Spacing.xs) {
                    ForEach(visible[start ..< min(start + Self.columns, visible.count)]) { slot in
                        TimeSlotChip(
                            slot: slot,
                            selected: slot.time == selectedTime,
                            expressLabel: expressLabel,
                            action: { onSelect(slot.time) }
                        )
                    }
                }
            }
            // The grid marks an express slot with a bolt; this says what the bolt costs.
            if visible.contains(where: { $0.state == .express }) {
                HStack(spacing: 6) {
                    Image(systemName: "bolt.fill")
                        .font(.system(size: 12))
                    Text(expressLabel)
                        .font(CleansiaTypography.labelSmall)
                        .fontWeight(.semibold)
                }
                .foregroundColor(TimeSlotChip.expressOrange)
                .accessibilityElement(children: .combine)
            }
        }
        .onChange(of: selectedTime) { _ in browsed = nil }
        .onChange(of: resetKey) { _ in browsed = nil }
    }
}

/// A part-of-day button: its name over its first and last arrival ("08:00–11:45").
private struct DayPartButton: View {
    let part: DayPartSlots
    let active: Bool
    let holdsSelection: Bool
    let action: () -> Void

    private var name: String {
        switch part.part {
        case .morning: L10n.Booking.dayPartMorning
        case .afternoon: L10n.Booking.dayPartAfternoon
        case .evening: L10n.Booking.dayPartEvening
        }
    }

    private var range: String {
        "\(part.slots.first?.time ?? "")–\(part.slots.last?.time ?? "")"
    }

    var body: some View {
        let enabled = part.isBookable
        Button(action: action) {
            VStack(spacing: 2) {
                HStack(spacing: Spacing.xxs) {
                    Text(name)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(active ? CleansiaColors.primaryText : CleansiaColors.onSurface)
                        .multilineTextAlignment(.center)
                    if holdsSelection {
                        Circle()
                            .fill(CleansiaColors.primary)
                            .frame(width: 6, height: 6)
                    }
                }
                Text(range)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .opacity(enabled ? 1 : 0.38)
            .padding(.horizontal, Spacing.xxs)
            .padding(.vertical, Spacing.xs)
            .frame(maxWidth: .infinity, minHeight: 48, maxHeight: .infinity)
            .background(active ? DayPartTimePicker.selectionTint : CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(active ? CleansiaColors.primary : CleansiaColors.outlineVariant, lineWidth: active ? 2 : 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        // "Morning, 6 slots available" — the range is what the grid then shows.
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(name + ", " + L10n.Booking.dayPartSlotsAvailable(part.bookableCount))
        .accessibilityAddTraits(active ? [.isButton, .isSelected] : .isButton)
    }
}

/// One arrival time in the grid: the time, with a bolt when it is an express slot.
private struct TimeSlotChip: View {
    let slot: BookingTimeSlot
    let selected: Bool
    let expressLabel: String
    let action: () -> Void

    static let expressOrange = Color(red: 0.918, green: 0.345, blue: 0.047)

    private var enabled: Bool {
        slot.state != .unavailable
    }

    private var textColor: Color {
        if selected { return CleansiaColors.primary }
        return enabled ? CleansiaColors.onSurface : CleansiaColors.onSurface.opacity(0.38)
    }

    private var background: Color {
        if selected { return DayPartTimePicker.selectionTint }
        return enabled ? CleansiaColors.surface : CleansiaColors.surfaceVariant.opacity(0.4)
    }

    var body: some View {
        Button(action: action) {
            HStack(spacing: 2) {
                if slot.state == .express {
                    Image(systemName: "bolt.fill")
                        .font(.system(size: 10))
                        .foregroundColor(Self.expressOrange)
                }
                Text(slot.time)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(textColor)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
            }
            .frame(maxWidth: .infinity, minHeight: 48)
            .background(background)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(slot.state == .express ? slot.time + ", " + expressLabel : slot.time)
        .accessibilityAddTraits(selected ? [.isButton, .isSelected] : .isButton)
    }
}

extension DayPartTimePicker {
    /// The selected chip's wash — Android's `selectionTint`: a faint primary on either theme.
    static let selectionTint = CleansiaColors.primary.opacity(0.14)
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
