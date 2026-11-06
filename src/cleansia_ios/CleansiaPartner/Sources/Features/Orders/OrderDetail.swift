import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct OrderDetail: Equatable {
    let id: String
    let orderNumber: String
    let status: OrderStatus?
    let cleaningDateTime: Date?
    /// The authoritative completion stamp (DB column, written inside the domain);
    /// the timer hero anchors the job duration on it instead of the wall clock.
    let completedAt: Date?
    let pay: Double?
    let currencyCode: String?
    let currencySymbol: String?

    let location: OrderLocation
    var area: String?
    let customerName: String?
    let customerPhone: String?

    let rooms: Int
    let bathrooms: Int
    /// Nil when the wire carried no level — a server that predates it.
    let dirtinessLevel: DirtinessLevel?
    let crew: OrderCrew?
    let seats: [OrderSeat]
    let workContractAcceptances: [WorkContractAcceptance]
    let services: [OrderDetailService]
    let packages: [OrderDetailPackage]
    let extras: [String]

    let customerNotes: String?
    let specialInstructions: String?
    let accessInstructions: String?
    /// How the cleaner gets in, and where: each arrives from Confirmed and each may be null.
    var floor: String?
    var flat: String?
    var accessMode: String?

    let payment: OrderDetailPayment

    let isAssignedToCurrentUser: Bool
    let hasAfterPhotos: Bool

    let lockoutReportedAt: Date?
    let lockoutCallAttempts: String?

    let orderNotes: [OrderNoteDto]
    let orderIssues: [OrderIssueDto]
    let statusHistory: [OrderStatusTrackDto]
    var estimatedMinutes: Int?
    /// One of the five keys the system cancels with; null is the customer or an administrator.
    var cancellationReason: String?
}

/// Name + the stable backend id: the checklist persists ticks under id-based keys
/// (Android parity — order-independent), and the id is optional only because the
/// wire marks it nullable; a nil id falls back to name-keying.
struct OrderDetailService: Equatable, Hashable {
    let id: String?
    let name: String
    let translations: [String: Translation]?

    init(id: String?, name: String, translations: [String: Translation]? = nil) {
        self.id = id
        self.name = name
        self.translations = translations
    }
}

struct OrderDetailPackage: Equatable, Hashable {
    let id: String?
    let name: String
    let price: Double?
    let translations: [String: Translation]?

    init(id: String?, name: String, price: Double?, translations: [String: Translation]? = nil) {
        self.id = id
        self.name = name
        self.price = price
        self.translations = translations
    }
}

/// One crew entry: `id` is the SEAT (`OrderEmployee.Id`), which is what an acceptance names. The name and phone
/// are the assigned crew's view of each other; past the job's 24 hours the server sends a given name only.
/// `completionConfirmedAt` is when its cleaner marked their part of the job done.
///
/// **Drop the row.** A seat with no id or no employee id pairs with nothing, while refusing would blank a
/// detail the server answered correctly for every other member. A dropped seat also leaves the crew count
/// `crewFinish` reads, which costs nothing: the server sends both ids on every seat, and Complete stays
/// guarded by `order.crew_not_done`.
struct OrderSeat: Equatable {
    let id: String
    let employeeId: String
    let completionConfirmedAt: Date?
    var fullName: String?
    var phone: String?

    init(
        id: String,
        employeeId: String,
        completionConfirmedAt: Date? = nil,
        fullName: String? = nil,
        phone: String? = nil
    ) {
        self.id = id
        self.employeeId = employeeId
        self.completionConfirmedAt = completionConfirmedAt
        self.fullName = fullName
        self.phone = phone
    }

    init?(_ dto: AssignedEmployeeDto) {
        guard let id = dto.id, !id.isBlank, let employeeId = dto.employeeId, !employeeId.isBlank else { return nil }
        self.id = id
        self.employeeId = employeeId
        completionConfirmedAt = dto.completionConfirmedAt
        fullName = dto.fullName
        phone = dto.phoneNumber
    }

    /// The first word of the name, as the server's own redaction cuts it.
    var givenName: String? {
        guard let name = fullName?.trimmingCharacters(in: .whitespaces), !name.isEmpty else { return nil }
        return name.components(separatedBy: " ").first
    }

    var initials: String {
        let words = (fullName ?? "").components(separatedBy: " ").filter { !$0.isEmpty }
        return words.prefix(2).compactMap(\.first).map { String($0).uppercased() }.joined()
    }
}

/// A current seat's accepted contract for work, with no name — the crew entry it pairs with by seat id
/// is where the (already masked) identity lives.
///
/// **Drop the row.** An acceptance with no id cannot be read, one with no seat id pairs with nothing,
/// and one with no instant or no version has no line to write — the version is a NOT NULL column, so
/// a blank is a broken wire, and the sheet's mapper refuses the same member. Any of them is the pending
/// state to this screen, never a claim.
struct WorkContractAcceptance: Equatable {
    let id: String
    let orderEmployeeId: String
    let acceptedOn: Date
    let documentVersion: String

    init(id: String, orderEmployeeId: String, acceptedOn: Date, documentVersion: String) {
        self.id = id
        self.orderEmployeeId = orderEmployeeId
        self.acceptedOn = acceptedOn
        self.documentVersion = documentVersion
    }

    init?(_ dto: WorkContractAcceptanceDto) {
        guard let id = dto.id, !id.isBlank,
              let orderEmployeeId = dto.orderEmployeeId, !orderEmployeeId.isBlank,
              let acceptedOn = dto.acceptedOn,
              let documentVersion = dto.documentVersion, !documentVersion.isBlank
        else { return nil }
        self.id = id
        self.orderEmployeeId = orderEmployeeId
        self.acceptedOn = acceptedOn
        self.documentVersion = documentVersion
    }
}

/// Backend `PaymentType` / `PaymentStatus` wire values (the enum ordinals the
/// `Code.value` envelope carries), the surrogate for the missing generated Swift
/// enums the cash-collection gate and the payment labels compare against.
enum PaymentTypeCode: Int, CaseIterable {
    case cash = 1
    case card = 2
}

enum PaymentStatusCode: Int, CaseIterable {
    case pending = 1
    case paid = 2
    case failed = 3
    case refunded = 4
    case disputed = 5
    case partiallyRefunded = 6
}

struct OrderDetailPayment: Equatable {
    let subtotal: Double?
    let total: Double?
    let tierDiscount: Double?
    let membershipDiscount: Double?
    let promoDiscount: Double?
    let creditApplied: Double?
    /// What the customer still owes: the total less any credit. The cash the cleaner collects.
    var amountDue: Double?
    let typeCode: Int?
    let statusCode: Int?

    var hasBreakdown: Bool {
        guard let subtotal, let total else { return false }
        return subtotal != total
    }

    var isCash: Bool {
        typeCode == PaymentTypeCode.cash.rawValue
    }

    var isSettled: Bool {
        statusCode == PaymentStatusCode.paid.rawValue
    }

    var isPending: Bool {
        statusCode == PaymentStatusCode.pending.rawValue
    }

    /// `CompleteOrder.CardIsCharged` refuses a card job whose payment is not Paid, PartiallyRefunded or Refunded.
    var isCardNotCharged: Bool {
        guard typeCode == PaymentTypeCode.card.rawValue, let statusCode else { return false }
        let charged: [PaymentStatusCode] = [.paid, .partiallyRefunded, .refunded]
        return !charged.map(\.rawValue).contains(statusCode)
    }
}

extension OrderDetail {
    /// The cleaner is on this job and the job is live (Confirmed / OnTheWay / InProgress): the work
    /// tools show, and the photo rails with them. It is also the gate on the photo FETCH, because
    /// `GetOrderPhotos` serves only a caller the strict access check admits and every photo is a
    /// forwardable signed URL into a private home. One value for both, so a change to what is on
    /// screen cannot leave behind a request nobody makes — the property Android gets structurally by
    /// creating the photos view model inside the section itself.
    var showsWorkSections: Bool {
        isAssignedToCurrentUser && (status == ._2 || status == ._3 || status == ._4)
    }

    /// The caller's own photos outlive the work: read from Confirmed through Completed.
    var readsPhotos: Bool {
        showsWorkSections || (isAssignedToCurrentUser && status == ._5)
    }

    /// The server's photo windows (`OrderPhoto.MayBeAddedAt`): before and entrance photos from
    /// Confirmed through InProgress, after photos only while InProgress. Outside its window a rail is
    /// read-only.
    func photoWindowOpen(for type: PhotoType) -> Bool {
        switch type {
        case ._1, ._3: status == ._2 || status == ._3 || status == ._4
        case ._2: status == ._4
        }
    }

    /// What stands where the customer was on a job the caller worked, once the server has closed it to
    /// the crew (24 hours after completion, at once on cancellation). Nil while it is open, and for a
    /// caller who never had the details.
    var customerDetailsClosedNote: String? {
        guard isAssignedToCurrentUser, location.navigationTarget == nil else { return nil }
        switch status {
        case ._5: return L10n.Orders.customerDetailsClosedCompleted
        case ._6: return L10n.Orders.customerDetailsClosedCancelled
        case ._0, ._1, ._2, ._3, ._4, .none: return nil
        }
    }

    /// The fields `OrderPiiRedaction` blanks by caller class are rendered off their **own arrival**.
    /// The server decides disclosure on `CanAccessOrderAsync`; `isAssignedToCurrentUser` counts the
    /// assignment list. They disagree for the employee who booked this cleaning for their own home,
    /// and gating the render on the flag hides that person's own data from them.
    ///
    /// A blank is a redaction, not a value — and the redaction is **mixed** (ADR-0047 §D4 as amended):
    /// string scalars like this phone are blanked to `""`, the free text and the composites are set
    /// to `null`, the lists to `[]`. Neither `!= nil` nor `!= ""` covers that set, so every gate below
    /// tolerates null, empty and whitespace on the same field.
    var showsCustomerContact: Bool {
        !(customerPhone ?? "").isBlank
    }

    /// The lifecycle conjunct answers *when is a door code useful*, not *may this caller see it*, so
    /// it stays — without it the code sits on screen forever on a finished job.
    var showsAccessCard: Bool {
        !(accessInstructions ?? "").isBlank && (status == ._3 || status == ._4)
    }

    /// The record of what was reported during the job outlives the job, so there is no lifecycle
    /// term here — only the arrival of the record, or a live invitation to start one.
    var showsNotesAndIssues: Bool {
        !orderNotes.isEmpty || !orderIssues.isEmpty || canAddNotes
    }

    /// Writing is an action, so it fails closed on the assignment, and only while the job is under
    /// way — nothing is added before the cleaner is on their way.
    var canAddNotes: Bool {
        isAssignedToCurrentUser && (status == ._3 || status == ._4)
    }

    /// The cash the cleaner takes, the same figure in the head's flag, the block, the button and both confirms;
    /// nil when the wire carried none, so a confirmation asks without naming an amount rather than guessing one.
    var cashDueLabel: String? {
        guard let due = payment.amountDue, due > 0 else { return nil }
        return OrdersFormat.money(due, symbol: currencySymbol)
    }

    var cashNotPaidOwedLabel: String? {
        cashDueLabel
    }
}

extension OrderDetail {
    /// **Refuse.** One order, so there is no page to refuse and no row to drop. Identity is refused
    /// rather than synthesized because every action on this screen — take, start, complete, the
    /// photo fetch — is keyed by it, so a blank id produces a detail whose every button writes to
    /// nothing. `rooms`/`bathrooms` are the scope the cleaner sizes the job by and
    /// `isAssignedToCurrentUser`/`hasAfterPhotos` decide what is offered, so a coerced `0` or
    /// `false` is a claim, not a fallback.
    ///
    /// The collections keep `?? []`: an absent list and an empty one render identically here and
    /// falsify no arithmetic, because this screen sums nothing over them.
    init(_ item: OrderItem) throws {
        id = try item.id.requireNonBlank("id")
        orderNumber = try item.displayOrderNumber.requireNonBlank("displayOrderNumber")
        status = item.status
        cleaningDateTime = item.cleaningDateTime
        completedAt = item.completedAt
        estimatedMinutes = item.estimatedTime.flatMap { $0 > 0 ? $0 : nil }
        pay = item.estimatedCleanerPay
        currencyCode = item.currency?.code ?? item.currency?.symbol
        currencySymbol = OrdersFormat.symbolOf(symbol: item.currency?.symbol, code: item.currency?.code)

        location = OrderLocation(item)
        area = item.customerAddressApproximate.flatMap { $0.isBlank ? nil : $0 }
        customerName = item.customerName
        customerPhone = item.customerPhone

        rooms = try item.rooms.require("rooms")
        bathrooms = try item.bathrooms.require("bathrooms")
        dirtinessLevel = item.dirtinessLevel
        crew = try OrderCrew(item)
        seats = item.assignedEmployees?.compactMap(OrderSeat.init) ?? []
        workContractAcceptances = item.workContractAcceptances?.compactMap(WorkContractAcceptance.init) ?? []
        services = item.selectedServices?.compactMap { service in
            service.name.flatMap { $0.isEmpty ? nil : $0 }
                .map { OrderDetailService(id: service.id, name: $0, translations: service.translations) }
        } ?? []
        packages = item.selectedPackages?.map { pkg in
            OrderDetailPackage(
                id: pkg.id,
                name: pkg.name.flatMap { $0.isEmpty ? nil : $0 } ?? "—",
                price: pkg.price,
                translations: pkg.translations
            )
        } ?? []
        extras = item.extras?.filter(\.value).keys.sorted() ?? []

        customerNotes = item.notes
        specialInstructions = item.specialInstructions
        accessInstructions = item.accessInstructions
        floor = item.customerFloor
        flat = item.customerApartment
        accessMode = item.accessMode
        cancellationReason = item.systemCancellationReason

        payment = OrderDetailPayment(
            subtotal: item.originalSubtotal,
            total: item.totalPrice,
            tierDiscount: item.tierDiscountAmount,
            membershipDiscount: item.membershipDiscountAmount,
            promoDiscount: item.promoDiscountAmount,
            creditApplied: item.creditAppliedAmount,
            amountDue: item.amountDueOnCard,
            typeCode: item.paymentType?.value,
            statusCode: item.paymentStatus?.value
        )

        isAssignedToCurrentUser = try item.isAssignedToCurrentUser.require("isAssignedToCurrentUser")
        hasAfterPhotos = try item.hasAfterPhotos.require("hasAfterPhotos")

        lockoutReportedAt = item.lockoutReportedAt
        lockoutCallAttempts = item.lockoutCallAttempts

        orderNotes = item.orderNotes ?? []
        orderIssues = item.orderIssues ?? []
        statusHistory = item.statusHistory ?? []
    }
}

extension OrderDetail {
    var bookedEnd: Date? {
        cleaningDateTime.map { $0.addingTimeInterval(TimeInterval(estimatedMinutes ?? 0) * 60) }
    }

    /// The earliest time the history reached `status`: a job bounced back and restarted keeps its first start.
    func firstStamp(_ status: OrderStatus) -> Date? {
        statusHistory.filter { $0.statusEnum == status }.compactMap(\.createdOn).min()
    }

    /// `completedAt` is the column the domain writes; the last Completed entry stands in for rows that predate it.
    var finishedAt: Date? {
        completedAt ?? statusHistory.filter { $0.statusEnum == ._5 }.compactMap(\.createdOn).max()
    }

    var cancelledAt: Date? {
        statusHistory.filter { $0.statusEnum == ._6 }.compactMap(\.createdOn).max()
    }

    /// Two or more on the job: each marks their own part, and the on-the-way and start stamps are the order's.
    var isCrewJob: Bool {
        seats.count >= 2
    }

    func ownSeat(_ myEmployeeId: String?) -> OrderSeat? {
        seats.first { $0.employeeId == myEmployeeId }
    }

    /// Everyone else on the job, in the order the server lists them.
    func mates(_ myEmployeeId: String?) -> [OrderSeat] {
        seats.filter { $0.employeeId != myEmployeeId }
    }

    /// The crew-mate the job waits for once her own part is marked: the first whose part is still open. Call goes
    /// to her only while she has not marked, and only with a phone the server sent.
    func openMate(_ myEmployeeId: String?) -> OrderSeat? {
        guard ownSeat(myEmployeeId)?.completionConfirmedAt != nil else { return nil }
        return mates(myEmployeeId).first { $0.completionConfirmedAt == nil }
    }

    /// The facts the status line and the run sheet are built from: her own mark, never a crew-mate's; the first
    /// after photo from the photos when they have loaded, else the server's flag.
    func runFacts(myEmployeeId: String?, photos: [OrderPhoto]) -> OrdersBoardRules.RunFacts? {
        guard let start = cleaningDateTime, let end = bookedEnd, [._2, ._3, ._4, ._5, ._6].contains(status) else {
            return nil
        }
        return OrdersBoardRules.RunFacts(
            status: status,
            start: start,
            end: end,
            crew: isCrewJob,
            ownMark: ownSeat(myEmployeeId)?.completionConfirmedAt,
            hasAfterPhoto: hasAfterPhotos || photos.contains { $0.photoType == ._2 },
            lockoutReportedAt: lockoutReportedAt,
            finishedAt: finishedAt,
            cancelledAt: cancelledAt,
            cancellationReason: cancellationReason
        )
    }

    /// Mode, floor and flat, each left out when the server sent none.
    var accessFacts: [String] {
        [L10n.Orders.accessMode(accessMode)].compactMap { $0 } + floorAndFlat
    }

    var floorAndFlat: [String] {
        [
            floor.flatMap { $0.isBlank ? nil : L10n.Orders.floor($0) },
            flat.flatMap { $0.isBlank ? nil : L10n.Orders.flat($0) }
        ].compactMap { $0 }
    }

    /// The package or service, rooms, baths, then the extras: the board's scope line.
    func scopeItems(locale: Locale) -> [String] {
        [
            packages.first.map { $0.localizedName(for: locale) } ?? services.first
                .map { $0.localizedName(for: locale) },
            rooms > 0 ? L10n.Orders.rooms(rooms) : nil,
            bathrooms > 0 ? L10n.Orders.baths(bathrooms) : nil,
            OrdersBoardRules.extrasLine(extras)
        ].compactMap { $0 }
    }

    /// Floor, flat and mode show from Confirmed; the access text itself from On the way (`showsAccessCard`).
    var showsAccessFacts: Bool {
        isAssignedToCurrentUser && [._2, ._3, ._4].contains(status)
    }

    /// The Getting-in step's "Cannot get in?" line: from On the way, and on Confirmed once the start has passed.
    func showsLockoutLine(now: Date) -> Bool {
        switch status {
        case ._3: true
        case ._2: cleaningDateTime.map { now >= $0 } ?? false
        default: false
        }
    }

    /// A job not taken whose crew has started: one named crew-mate is "Marta is already on it", anything else the
    /// board's count.
    var onItLine: String? {
        guard !seats.isEmpty else { return nil }
        if seats.count == 1, let name = seats[0].givenName { return L10n.Orders.mateOnIt(name) }
        return L10n.Orders.crewTaken(seats.count)
    }

    /// What a cancelled job pays, by why it was cancelled; a lockout the company confirmed says so in bold. With no
    /// reason it was the customer or an administrator, and the general fee rule holds whoever it was.
    var cancelledPayLine: (text: String, bold: Bool) {
        guard let reason = cancellationReason, L10n.Orders.cancellationReasons.contains(reason) else {
            return (L10n.WorkContract.summaryLateCancel, false)
        }
        return reason == "order.cancelled.customer_lockout"
            ? (L10n.Orders.cancelLockoutPaid, true)
            : (L10n.Orders.cancelNoFee, false)
    }

    /// While the customer's details are still shown on a completed job (24 hours): when they close, "tomorrow" on
    /// the day it was finished and "today" on the next.
    func detailsCloseLine(now: Date, locale: Locale, calendar: Calendar = .current) -> String? {
        guard status == ._5, location.navigationTarget != nil, let finishedAt else { return nil }
        let time = OrdersFormat.timeOnly(finishedAt, locale: locale)
        return calendar.isDate(now, inSameDayAs: finishedAt)
            ? L10n.Orders.detailsCloseTomorrow(time)
            : L10n.Orders.detailsCloseToday(time)
    }

    /// "possible until the start, 14:00", the weekday too when the start is another day.
    func dropUntil(now: Date, locale: Locale, calendar: Calendar = .current) -> String? {
        guard let start = cleaningDateTime else { return nil }
        let time = OrdersFormat.timeOnly(start, locale: locale)
        return L10n.Orders.dropPossibleUntil(calendar.isDate(start, inSameDayAs: now)
            ? time
            : "\(OrdersFormat.pattern("EEE", start, locale: locale)) \(time)")
    }
}

extension OrderDetail {
    /// What the board's row already says, drawn at once while the job loads (and kept when the rest of it fails):
    /// the head, the run sheet's labels, the scope and the contact. Floor, flat and the customer's words come only
    /// with the job.
    init?(row: OrderListItem, mine: Bool) {
        guard let id = row.id, !id.isBlank else { return nil }
        self.init(
            id: id,
            orderNumber: row.displayOrderNumber ?? "",
            status: row.status,
            cleaningDateTime: row.cleaningDateTime,
            completedAt: nil,
            pay: row.estimatedCleanerPay,
            currencyCode: row.currency?.code ?? row.currency?.symbol,
            currencySymbol: OrdersFormat.symbolOf(row.currency),
            location: Self.location(of: row),
            area: OrdersBoardRules.areaLine(row),
            customerName: row.customerName,
            customerPhone: row.customerPhone,
            rooms: row.rooms ?? 0,
            bathrooms: row.bathrooms ?? 0,
            dirtinessLevel: row.dirtinessLevel,
            crew: Self.crew(of: row),
            seats: [],
            workContractAcceptances: [],
            services: row.selectedServices?.compactMap { service in
                service.name.map { OrderDetailService(id: service.id, name: $0, translations: service.translations) }
            } ?? [],
            packages: row.selectedPackages?.compactMap { package in
                package.name.map {
                    OrderDetailPackage(
                        id: package.id,
                        name: $0,
                        price: package.price,
                        translations: package.translations
                    )
                }
            } ?? [],
            extras: OrdersBoardRules.extraSlugs(row.extras),
            customerNotes: nil,
            specialInstructions: nil,
            accessInstructions: nil,
            payment: OrderDetailPayment(
                subtotal: row.originalSubtotal,
                total: row.totalPrice,
                tierDiscount: nil,
                membershipDiscount: nil,
                promoDiscount: nil,
                creditApplied: row.creditAppliedAmount,
                amountDue: row.amountDueOnCard,
                typeCode: row.paymentType?.value,
                statusCode: row.paymentStatus?.value
            ),
            isAssignedToCurrentUser: mine,
            hasAfterPhotos: false,
            lockoutReportedAt: nil,
            lockoutCallAttempts: nil,
            orderNotes: [],
            orderIssues: [],
            statusHistory: [],
            estimatedMinutes: row.estimatedTime.flatMap { $0 > 0 ? $0 : nil }
        )
    }

    private static func location(of row: OrderListItem) -> OrderLocation {
        guard OrdersBoardRules.streetLine(row.customerAddress) != nil, let line = row.customerAddress else {
            return OrdersBoardRules.areaLine(row).map(OrderLocation.approximate) ?? .none
        }
        let coordinate = row.customerAddressLatitude.flatMap { lat in
            row.customerAddressLongitude.map { Coordinate(latitude: lat, longitude: $0) }
        }
        return .precise(.init(line: line, coordinate: coordinate))
    }

    private static func crew(of row: OrderListItem) -> OrderCrew? {
        let size = row.requiredEmployees ?? 0
        let open = row.availableSpots ?? 0
        guard size > 0 else { return nil }
        return row.hasAvailableSpots == true && open > 0 ? .spotsOpen(crewSize: size, openSpots: open)
            : .full(crewSize: size)
    }
}
