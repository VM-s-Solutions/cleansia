import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The open jobs of the next seven days, a column a day from today: the weekday heading it, the day a square card
/// (her own job's day ringed, a day with open jobs on the tint), and under it the open count the board has loaded
/// for it, a real zero included, its line left blank while the open jobs are still loading. Each day is one
/// VoiceOver element: its date, her job, its count.
struct OrdersWeekStrip: View {
    @Environment(\.locale) private var locale
    let cells: [OrdersBoardRules.WeekDay]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(alignment: .top, spacing: 6) {
                ForEach(cells.indices, id: \.self) { index in
                    cell(cells[index])
                }
            }
            if cells.contains(where: \.mine) {
                HStack(alignment: .firstTextBaseline, spacing: 6) {
                    RoundedRectangle(cornerRadius: 3)
                        .fill(CleansiaColors.primary.opacity(0.12))
                        .overlay(RoundedRectangle(cornerRadius: 3).strokeBorder(CleansiaColors.primary, lineWidth: 1.5))
                        .frame(width: 10, height: 10)
                        .xHeightAligned(of: CleansiaTypography.scale.labelMedium)
                    Text(L10n.Orders.weekLegendYourJob)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .accessibilityHidden(true)
            }
        }
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
    }

    private func cell(_ cell: OrdersBoardRules.WeekDay) -> some View {
        VStack(spacing: Spacing.xxs) {
            Text(OrdersFormat.pattern("EEE", cell.day, locale: locale))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Color.clear
                .aspectRatio(1, contentMode: .fit)
                .overlay {
                    Text(OrdersDateTile.day(cell.day, locale: locale))
                        .font(CleansiaTypography.titleMedium)
                        .monospacedDigit()
                        .foregroundColor(
                            cell.mine || cell.hasOpen ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant
                        )
                }
                .background(fill(cell), in: RoundedRectangle(cornerRadius: CornerRadius.small))
                .overlay {
                    if cell.mine {
                        RoundedRectangle(cornerRadius: CornerRadius.small)
                            .strokeBorder(CleansiaColors.primary, lineWidth: 1.5)
                    }
                }
            Text(verbatim: cell.open.map { "\($0)" } ?? " ")
                .font(CleansiaTypography.labelMedium)
                .monospacedDigit()
                .foregroundColor(cell.hasOpen ? CleansiaColors.primaryText : CleansiaColors.onSurfaceVariant)
        }
        .lineLimit(1)
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(OrdersBoardRules.weekCellLabel(
            day: cell.day,
            open: cell.open,
            mine: cell.mine,
            locale: locale
        ))
    }

    private func fill(_ cell: OrdersBoardRules.WeekDay) -> Color {
        cell.mine || cell.hasOpen ? CleansiaColors.primary.opacity(0.12) : CleansiaColors.surfaceVariant
    }
}

/// History's month as a grid of square days, weeks from Monday: a worked day on the tint with one pip per job hung 4
/// above its foot, so every digit of a week sits on one line; today ringed; days outside the period or still to come
/// quiet. Hidden from VoiceOver: the ledger under it says it.
struct OrdersMonthGrid: View {
    @Environment(\.locale) private var locale
    let cells: [OrdersBoardRules.GridDay]

    var body: some View {
        LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: Spacing.xxs), count: 7), spacing: 4) {
            ForEach(Self.weekdays(locale: locale), id: \.self) { weekday in
                Text(weekday)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .lineLimit(1)
                    .padding(.bottom, Spacing.hair)
            }
            ForEach(cells.indices, id: \.self) { index in
                cell(cells[index])
            }
        }
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
        .accessibilityHidden(true)
    }

    private func cell(_ cell: OrdersBoardRules.GridDay) -> some View {
        let quiet = !cell.inPeriod || cell.isFuture
        let worked = cell.jobs > 0 && !quiet
        return Color.clear
            .aspectRatio(1, contentMode: .fit)
            .overlay {
                Text(OrdersDateTile.day(cell.day, locale: locale))
                    .font(CleansiaTypography.bodyMedium)
                    .monospacedDigit()
                    .foregroundColor(quiet ? CleansiaColors.quiet : CleansiaColors.onSurface)
            }
            .overlay(alignment: .bottom) {
                if worked {
                    GeometryReader { space in
                        let pips = OrdersBoardRules.pipsThatFit(cell.jobs, width: space.size.width)
                        HStack(spacing: 3) {
                            ForEach(0 ..< pips, id: \.self) { _ in
                                Circle().fill(CleansiaColors.primary).frame(width: 5, height: 5)
                            }
                        }
                        .frame(width: space.size.width)
                    }
                    .frame(height: 5)
                    .padding(.bottom, Spacing.xxs)
                }
            }
            .background(
                worked ? CleansiaColors.primary.opacity(0.12) : .clear,
                in: RoundedRectangle(cornerRadius: CornerRadius.small)
            )
            .overlay {
                if cell.isToday {
                    RoundedRectangle(cornerRadius: CornerRadius.small)
                        .strokeBorder(CleansiaColors.primary, lineWidth: 1.5)
                }
            }
    }

    /// Two letters a day from Monday, in the app locale: Mo Tu We, Po Út St, Пн Вт Ср.
    static func weekdays(locale: Locale) -> [String] {
        var calendar = Calendar(identifier: .gregorian)
        calendar.locale = locale
        let symbols = calendar.shortStandaloneWeekdaySymbols
        return (Array(symbols.dropFirst()) + symbols.prefix(1)).map { String($0.prefix(2)).capitalized(with: locale) }
    }
}

extension OrdersBoardRules {
    /// One pip per job, as many whole 5-pt pips 3 pt apart as the cell is wide: the grid never widens or spills.
    static func pipsThatFit(_ jobs: Int, width: CGFloat) -> Int {
        min(max(jobs, 0), max(Int((width + 3) / 8), 0))
    }

    /// One day of the week strip: the open jobs loaded for it, and whether she has a job that day.
    struct WeekDay: Equatable {
        let day: Date
        let open: Int?
        let mine: Bool

        var hasOpen: Bool {
            (open ?? 0) > 0
        }
    }

    /// One day of History's grid.
    struct GridDay: Equatable {
        let day: Date
        let inPeriod: Bool
        let jobs: Int
        let isToday: Bool
        let isFuture: Bool
    }

    static func weekStrip(
        now: Date,
        open: [OrderListItem]?,
        own: [OrderListItem],
        calendar: Calendar = .current
    ) -> [OrdersBoardRules.WeekDay] {
        let today = calendar.startOfDay(for: now)
        let onDay = { (row: OrderListItem, day: Date) in
            row.cleaningDateTime.map { calendar.isDate($0, inSameDayAs: day) } ?? false
        }
        return (0 ..< 7).compactMap { offset in
            guard let day = calendar.date(byAdding: .day, value: offset, to: today) else { return nil }
            return WeekDay(
                day: day,
                open: open.map { rows in rows.filter { onDay($0, day) }.count },
                mine: own.contains { onDay($0, day) }
            )
        }
    }

    /// The Available badge and *See N open jobs*: the server's total, less the loaded rows whose booked slot has
    /// ended, which it still counts. Nil until the server answers.
    static func openCount(total: Int?, rows: [OrderListItem], now: Date) -> Int? {
        total.map { max($0 - rows.filter { isExpired($0, now: now) }.count, 0) }
    }

    /// "Thursday, November 12, your job, 0 open jobs".
    static func weekCellLabel(day: Date, open: Int?, mine: Bool, locale: Locale) -> String {
        ([OrdersFormat.weekdayLongDay(day, locale: locale)]
            + (mine ? [L10n.Orders.weekLegendYourJob] : [])
            + (open.map { [L10n.Orders.weekOpenCount($0)] } ?? []))
            .joined(separator: ", ")
    }

    /// The days History's grid shows: the period, stopped at this week's Sunday while the period is running.
    static func historyGridRange(
        _ period: CompletedPeriod,
        now: Date,
        calendar: Calendar = .current
    ) -> (from: Date, through: Date)? {
        let range = OrdersQueryBuilder.periodToDateRange(period, calendar: calendar, now: now)
        guard let from = range.from, let to = range.to,
              let last = calendar.date(byAdding: .day, value: -1, to: to)
        else { return nil }
        var weeks = calendar
        weeks.firstWeekday = 2
        let today = calendar.startOfDay(for: now)
        guard today >= from, today < to, let weekEnd = weeks.dateInterval(of: .weekOfYear, for: today)?.end,
              let sunday = calendar.date(byAdding: .day, value: -1, to: weekEnd)
        else { return (from, last) }
        return (from, min(last, sunday))
    }

    /// Whole weeks from the Monday on or before `from` to the Sunday on or after `through`.
    static func monthGrid(
        from: Date,
        through: Date,
        rows: [OrderListItem],
        now: Date,
        calendar: Calendar = .current
    ) -> [OrdersBoardRules.GridDay] {
        var weeks = calendar
        weeks.firstWeekday = 2
        guard let start = weeks.dateInterval(of: .weekOfYear, for: from)?.start,
              let end = weeks.dateInterval(of: .weekOfYear, for: through)?.end
        else { return [] }
        let today = calendar.startOfDay(for: now)
        var cells: [OrdersBoardRules.GridDay] = []
        var day = start
        while day < end {
            let jobs = rows.filter { $0.cleaningDateTime.map { calendar.isDate($0, inSameDayAs: day) } ?? false }.count
            cells.append(GridDay(
                day: day,
                inPeriod: day >= from && day <= through,
                jobs: jobs,
                isToday: day == today,
                isFuture: day > today
            ))
            guard let next = calendar.date(byAdding: .day, value: 1, to: day) else { break }
            day = next
        }
        return cells
    }
}
