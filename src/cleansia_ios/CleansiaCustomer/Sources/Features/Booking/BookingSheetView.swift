import CleansiaCore
import SwiftUI

struct BookingSheetView: View {
    @ObservedObject var vm: BookingViewModel
    @Environment(\.snackbarController) private var snackbar
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @State private var success: BookingSuccess?
    @State private var holdsBusyOverlay = false
    @State private var busyShownAt = ContinuousClock.now
    @State private var revealed = true
    @State private var mascotTravels = false
    @State private var busyMascotFrame: CGRect?
    @State private var heroMascotFrame: CGRect?
    @State private var draftHeroFrame: CGRect?
    @State private var sheetHeight: CGFloat = 0
    @State private var busyMascotSettles = ContinuousClock.now
    @State private var flight: (from: CGRect, to: CGRect)?
    @State private var flightLanded = false
    @State private var flightArtTurned = false
    @State private var slideResetCount = 0
    @State private var owesMoney = false
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let serviceArea: ServiceAreaProvider?
    let paymentSheet: PaymentSheetPresenting
    let orderClient: OrderClient
    let warmOrders: @Sendable () async -> Void
    let onDismiss: () -> Void
    let onViewOrder: (String) -> Void
    let onCompleteProfile: () -> Void
    let onOpenPayments: () -> Void
    private let submitsOnAppear: Bool

    private static let footerSnackbarInset: CGFloat = 88
    private static let detailsWait = Duration.milliseconds(1200)
    private static let minimumBusy = Duration.milliseconds(600)
    private static let flightLength = 0.42

    init(
        vm: BookingViewModel,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider? = nil,
        paymentSheet: PaymentSheetPresenting,
        orderClient: OrderClient,
        warmOrders: @escaping @Sendable () async -> Void = {},
        onDismiss: @escaping () -> Void,
        onViewOrder: @escaping (String) -> Void = { _ in },
        onCompleteProfile: @escaping () -> Void = {},
        onOpenPayments: @escaping () -> Void = {},
        initialSuccess: BookingSuccess? = nil,
        submitsOnAppear: Bool = false
    ) {
        self.vm = vm
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.paymentSheet = paymentSheet
        self.orderClient = orderClient
        self.warmOrders = warmOrders
        self.onDismiss = onDismiss
        self.onViewOrder = onViewOrder
        self.onCompleteProfile = onCompleteProfile
        self.onOpenPayments = onOpenPayments
        self.submitsOnAppear = submitsOnAppear
        _success = State(initialValue: initialSuccess)
    }

    var body: some View {
        Group {
            if let success {
                BookingSuccessView(
                    success,
                    loadOrder: { [orderClient] id in
                        if case let .success(order) = await orderClient.getById(orderId: id) {
                            return order
                        }
                        return nil
                    },
                    warmOrders: warmOrders,
                    revealed: revealed,
                    mascotTravels: mascotTravels,
                    onViewOrder: success.orderId.isBlank ? nil : {
                        vm.reset()
                        self.success = nil
                        onViewOrder(success.orderId)
                    },
                    onDone: {
                        vm.reset()
                        self.success = nil
                        onDismiss()
                    },
                    onDetailsSettled: { Task { await reveal() } },
                    onHeroMascotFrame: { heroMascotFrame = $0 }
                )
                .transition(.identity)
            } else {
                BookingSheetContent(
                    viewModel: vm,
                    geocoding: geocoding,
                    mapProvider: mapProvider,
                    serviceArea: serviceArea,
                    slideResetTrigger: slideResetCount,
                    onLeading: {
                        if !vm.back() { onDismiss() }
                    },
                    onContinue: { vm.advance() },
                    onConfirm: submit
                )
                .transition(.identity)
            }
        }
        .background { draftConfirmation }
        .background(GeometryReader { proxy in
            Color.clear
                .onAppear { sheetHeight = proxy.size.height }
                .onChange(of: proxy.size.height) { sheetHeight = $0 }
        }
        .ignoresSafeArea(.keyboard))
        .overlay {
            BusyMascotOverlay(
                visible: vm.submitState.isSubmitting || holdsBusyOverlay,
                message: L10n.Booking.busyBooking,
                exit: reduceMotion ? .easeOut(duration: 0.15) : .timingCurve(0.3, 0, 1, 1, duration: 0.12),
                scrimExit: reduceMotion ? .easeOut(duration: 0.15) : .easeOut(duration: 0.3).delay(0.12),
                mascotFigureHeight: busyFigureHeight,
                mascotHidden: flight != nil,
                onMascotFrame: { busyMascotFrame = $0 }
            )
        }
        .overlay { flyer }
        .onChange(of: busyFigureHeight) { _ in
            busyMascotSettles = .now + .seconds(BusyMascotOverlay.mascotResize)
        }
        .task(id: submitsOnAppear) { if submitsOnAppear { await submit() } }
        .onReceive(vm.events) { event in
            switch event {
            case .selectionPrunedForMarket:
                snackbar.showInfo(L10n.Booking.marketSelectionPruned)
            case .cashCleared:
                snackbar.showInfo(L10n.Booking.cashCleared)
            }
        }
        .snackbarHost(snackbar, bottomInset: Self.footerSnackbarInset)
        .unpaidReceivableAlert(isPresented: $owesMoney, onPay: onOpenPayments)
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        // Swiping down closes the sheet on any step: the draft is the session's view model, so the Book
        // button reopens it where it was left. Only a booking being placed holds it, so its outcome has
        // somewhere to land.
        .interactiveDismissDisabled(vm.submitState.isSubmitting || holdsBusyOverlay)
    }

    private var busyFigureHeight: CGFloat {
        BookingSuccessView.busyFigureHeight(
            hero: heroMascotFrame,
            draft: draftHeroFrame,
            sheetHeight: sheetHeight,
            accessibilityText: dynamicTypeSize.isAccessibilitySize
        )
    }

    /// The confirmation this booking will arrive as, laid out unseen in its own place from the review step on: a
    /// compact sheet gives its hero the ticket's slack, which no size tier can tell, so the busy card reads the
    /// hero's height here before the order lands.
    @ViewBuilder private var draftConfirmation: some View {
        if success == nil, vm.currentStep == BookingStepGate.totalSteps {
            BookingSuccessView.unseen(Self.submittedDraft(
                vm.state,
                quote: vm.quoteState.quote,
                discount: vm.effectiveDiscount,
                catalog: vm.catalogState.loadedValue,
                extras: vm.extrasState.loadedValue,
                locale: locale
            )) { draftHeroFrame = $0 }
        }
    }

    static func submittedDraft(
        _ state: BookingState,
        quote: BookingQuote?,
        discount: Double,
        catalog: Catalog? = nil,
        extras: [CatalogExtra]? = nil,
        locale: Locale = .current
    ) -> BookingSuccess {
        let summary = BookingPriceSummary.resolve(
            quote: quote,
            discount: discount,
            payByCard: state.paymentMethod == .card
        )
        let services = catalog?.services.filter { state.selectedServiceIds.contains($0.id) }
            .map { $0.localizedName(for: locale) } ?? []
        let packages = catalog?.packages.filter { state.selectedPackageIds.contains($0.id) }
            .map { $0.localizedName(for: locale) } ?? []
        return BookingSuccess(
            orderId: "",
            confirmationCode: "",
            cleaningAt: state.selectedInstant,
            estimatedMinutes: quote?.estimatedMinutes ?? 0,
            street: state.street,
            city: state.city,
            appliesCredit: summary.creditApplied > 0,
            requiredEmployees: quote?.requiredEmployees ?? 1,
            rooms: state.rooms,
            bathrooms: state.bathrooms,
            dirtiness: quote?.dirtiness ?? state.dirtiness ?? .normal,
            basketNames: services + packages,
            extraNames: (extras ?? []).filter { state.selectedExtraSlugs.contains($0.slug) }
                .map { $0.localizedName(for: locale) },
            paysCash: state.paymentMethod == .cash
        )
    }

    private func submit() async {
        let draft = Self.submittedDraft(
            vm.state,
            quote: vm.quoteState.quote,
            discount: vm.effectiveDiscount,
            catalog: vm.catalogState.loadedValue,
            extras: vm.extrasState.loadedValue,
            locale: locale
        )
        busyShownAt = .now
        holdsBusyOverlay = true
        let outcome = await vm.submit()
        if case .success = outcome {} else { holdsBusyOverlay = false }
        await handle(outcome, draft: draft)
    }

    private func handle(_ outcome: BookingSubmitOutcome, draft: BookingSuccess) async {
        switch outcome {
        case let .success(orderId, confirmationCode):
            handOver(draft.placed(orderId: orderId, confirmationCode: confirmationCode))
        case let .cardPending(orderId, confirmationCode, presentation):
            await presentPaymentSheet(
                presentation,
                booking: draft.placed(orderId: orderId, confirmationCode: confirmationCode)
            )
        case .profileIncomplete:
            slideResetCount += 1
            onCompleteProfile()
        case .paymentMethodCleared:
            slideResetCount += 1
        case .owesMoney:
            slideResetCount += 1
            owesMoney = true
        case .timeNoLongerHolds:
            slideResetCount += 1
            snackbar.showInfo(L10n.Booking.draftTimeChanged)
        case let .failed(error):
            slideResetCount += 1
            // Prefer the server's own business error — "no cleaner is available
            // for that slot", "the price changed", "we don't serve that city" are
            // all actionable and already translated in all five locales, where the
            // generic network line tells the customer nothing. `showApiError`
            // drops cancellations by itself, so no extra guard is needed here.
            if let error {
                snackbar.showApiError(error)
            } else {
                snackbar.showError(L10n.Booking.errorGenericNetwork)
            }
        }
    }

    /// The busy overlay stays over the confirmation until its details are read or 1.2 s have passed, so
    /// the confirmation arrives complete rather than filling in after it is on screen.
    private func handOver(_ booking: BookingSuccess) {
        revealed = false
        holdsBusyOverlay = true
        success = booking
        awaitDetails()
    }

    private func awaitDetails() {
        Task {
            try? await Task.sleep(for: Self.detailsWait)
            await reveal()
        }
    }

    /// The cleaner leaves the busy card at the hero's height, so a card still growing to it is let finish first.
    private func reveal() async {
        try? await Task.sleep(until: busyShownAt + Self.minimumBusy, clock: .continuous)
        if !reduceMotion { try? await Task.sleep(until: busyMascotSettles, clock: .continuous) }
        guard success != nil, holdsBusyOverlay, !revealed, flight == nil else { return }
        if !reduceMotion, let from = busyMascotFrame, let to = heroMascotFrame {
            flightLanded = false
            flightArtTurned = false
            flight = (from, to)
            try? await Task.sleep(for: .milliseconds(16))
        }
        mascotTravels = flight != nil
        revealed = true
        holdsBusyOverlay = false
        guard flight != nil else { return }
        withAnimation(.timingCurve(0.05, 0.7, 0.1, 1, duration: Self.flightLength)) { flightLanded = true }
        withAnimation(.linear(duration: 0.14)) { flightArtTurned = true }
        try? await Task.sleep(for: .milliseconds(460))
        flight = nil
    }

    /// Each pose is drawn on its own canvas, so both stand as tall as the figure they leave and the one they land
    /// on: the cleaner from the busy card's canvas, the welcomer onto the hero's.
    @ViewBuilder private var flyer: some View {
        if let flight {
            GeometryReader { proxy in
                let origin = proxy.frame(in: .global).origin
                ZStack(alignment: .topLeading) {
                    standIn(
                        "mascot_cleaning_still",
                        from: flight.from,
                        to: MascotFigureBox.cleaning.canvas(standingAs: .welcoming, on: flight.to),
                        origin: origin
                    )
                    .opacity(flightArtTurned ? 0 : 1)
                    standIn(
                        "mascot_welcoming_start",
                        from: MascotFigureBox.welcoming.canvas(standingAs: .cleaning, on: flight.from),
                        to: flight.to,
                        origin: origin
                    )
                    .opacity(flightArtTurned ? 1 : 0)
                }
            }
            .allowsHitTesting(false)
            .accessibilityHidden(true)
        }
    }

    private func standIn(_ art: String, from: CGRect, to: CGRect, origin: CGPoint) -> some View {
        let frame = flightLanded ? to : from
        return Image(decorative: art).resizable()
            .frame(width: to.width, height: to.height)
            .scaleEffect(frame.width / to.width, anchor: .topLeading)
            .offset(x: frame.minX - origin.x, y: frame.minY - origin.y)
    }

    /// Stripe's sheet over the booking. The app leaves the foreground under it and comes back — a 3-D Secure
    /// approval in the bank app, a look-up of the card number, a Face ID prompt — and the order is past the
    /// time by then, so the open booking is not re-checked while it is up.
    private func showPaymentSheet(_ presentation: PaymentSheetPresentation) async -> PaymentSheetOutcome {
        vm.paymentSheetShowing = true
        defer { vm.paymentSheetShowing = false }
        return await paymentSheet.present(presentation)
    }

    private func presentPaymentSheet(_ presentation: PaymentSheetPresentation, booking: BookingSuccess) async {
        let outcome = await showPaymentSheet(presentation)
        switch BookingCardResultResolver.resolve(outcome, confirmationCode: booking.confirmationCode) {
        case let .navigateToSuccess(code):
            // Same duplicate-order guard as the VM's cash path: clear the
            // session-lived draft the moment the payment lands, not on the
            // success screen's exit (a swiped-away sheet skips those closures).
            // Stripe's sheet has already gone, so the overlay comes up whole, without its entrance, in the
            // frame the confirmation replaces the form.
            var cover = Transaction()
            cover.disablesAnimations = true
            withTransaction(cover) {
                vm.reset()
                busyShownAt = .now
                handOver(booking.placed(orderId: booking.orderId, confirmationCode: code))
            }
        case let .snackbar(messageKey):
            snackbar.showError(L10n.localized(messageKey))
        }
    }
}

private struct BookingSheetContent: View {
    @ObservedObject var viewModel: BookingViewModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let serviceArea: ServiceAreaProvider?
    let slideResetTrigger: Int
    let onLeading: () -> Void
    let onContinue: () -> Void
    let onConfirm: () async -> Void

    /// The direction of the last step change; the page coming in enters by it.
    @State private var movingForward = true
    /// The step on screen. It follows the wizard's one run-loop turn behind (the `onChange` in `body`).
    @State private var shownStep: Int

    init(
        viewModel: BookingViewModel,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider?,
        slideResetTrigger: Int,
        onLeading: @escaping () -> Void,
        onContinue: @escaping () -> Void,
        onConfirm: @escaping () async -> Void
    ) {
        self.viewModel = viewModel
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.slideResetTrigger = slideResetTrigger
        self.onLeading = onLeading
        self.onContinue = onContinue
        self.onConfirm = onConfirm
        _shownStep = State(initialValue: viewModel.currentStep)
    }

    private var step: Int {
        shownStep
    }

    /// Which way the step change being drawn goes: on, the next step comes in from the trailing edge; back, the
    /// previous one comes in from the leading edge (Android's `AnimatedContent` twin). While the wizard has
    /// moved and the page has not yet, the page on screen is drawn with the way it is about to leave.
    private var forward: Bool {
        viewModel.currentStep == shownStep ? movingForward : viewModel.currentStep > shownStep
    }

    private var isLastStep: Bool {
        step >= BookingStepGate.totalSteps
    }

    private var isSubmitting: Bool {
        viewModel.submitState.isSubmitting
    }

    private var canContinue: Bool {
        BookingStepGate.canContinue(step: step, state: viewModel.state, alreadyConsented: viewModel.alreadyConsented)
    }

    private var canConfirm: Bool {
        canContinue && !isSubmitting
    }

    /// On a card booking this is what the card is asked for — the figure the Stripe sheet then shows —
    /// so credit that applies is already off it.
    private var totalDisplay: String? {
        guard let quote = viewModel.quoteState.quote else { return nil }
        let summary = BookingPriceSummary.resolve(
            quote: quote,
            discount: viewModel.effectiveDiscount,
            payByCard: viewModel.state.paymentMethod == .card
        )
        return BookingPricing.formatTotal(summary.dueOnCard, currencyCode: quote.currencyCode)
    }

    var body: some View {
        VStack(spacing: 0) {
            header
            ProgressView(value: Double(step), total: Double(BookingStepGate.totalSteps))
                .tint(CleansiaColors.primary)
                .padding(.horizontal, Spacing.l)

            stepBody
                .frame(maxWidth: .infinity, maxHeight: .infinity)

            footer
        }
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await viewModel.loadConsentStatus() }
        // The direction is read from the step change itself, so the wizard's own step back to When — a time
        // that stopped holding, at the slide or on a return to the foreground — slides back as the back button
        // does. A page leaving animates with the transition it last rendered with, so the page drawn follows
        // the wizard one turn later, once the page on screen has been drawn with the way it leaves (`forward`).
        .onChange(of: viewModel.currentStep) { next in
            DispatchQueue.main.async {
                movingForward = next > shownStep
                shownStep = next
            }
        }
    }

    private var header: some View {
        HStack(spacing: Spacing.s) {
            Button {
                onLeading()
            } label: {
                Image(systemName: step > 1 ? "chevron.left" : "xmark")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel(Text(step > 1 ? L10n.Booking.back : L10n.Booking.close))

            Text(L10n.Booking.stepTitle(step))
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onBackground)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
                .frame(maxWidth: .infinity, alignment: .leading)

            Text(L10n.Booking.stepIndicator(step, BookingStepGate.totalSteps))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .lineLimit(1)
                .fixedSize()
                .contentTransition(.numericText())
                .animation(.default, value: step)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.l)
    }

    /// The page changes identity inside a ZStack of its own, so the page leaving stays on screen and slides out
    /// while the next slides in. With the identity at the top of the step area, the leaving page was dropped in
    /// one frame and only the incoming one moved.
    private var stepBody: some View {
        ZStack {
            stepPage
                .transition(stepTransition)
                .id(step)
        }
        .animation(.easeInOut(duration: 0.28), value: step)
    }

    @ViewBuilder private var stepPage: some View {
        switch step {
        case 1: ServicesStep(viewModel: viewModel)
        case 2: DirtinessStep(viewModel: viewModel)
        case 3: WhenWhereStep(
                viewModel: viewModel,
                geocoding: geocoding,
                mapProvider: mapProvider,
                serviceArea: serviceArea
            )
        default: ConfirmStep(viewModel: viewModel)
        }
    }

    /// Leading and trailing follow the layout direction, so back slides the right way in either. Reduce
    /// Motion crossfades instead.
    private var stepTransition: AnyTransition {
        if reduceMotion { return .opacity }
        return .asymmetric(
            insertion: .move(edge: forward ? .trailing : .leading).combined(with: .opacity),
            removal: .move(edge: forward ? .leading : .trailing).combined(with: .opacity)
        )
    }

    private var confirmLabel: String {
        totalDisplay.map(L10n.Booking.slideToConfirmPrice) ?? L10n.Booking.slideToConfirm
    }

    private var footer: some View {
        VStack(spacing: 0) {
            if isLastStep {
                SlideToConfirm(
                    idleLabel: confirmLabel,
                    busyLabel: confirmLabel,
                    isBusy: isSubmitting,
                    enabled: canConfirm,
                    resetTrigger: slideResetTrigger,
                    onConfirm: { Task { await onConfirm() } }
                )
                .lineLimit(1)
                .minimumScaleFactor(0.85)
            } else {
                CleansiaPrimaryButton(
                    totalDisplay.map(L10n.Booking.continuePrice) ?? L10n.Booking.continueAction,
                    trailingIcon: "arrow.right",
                    loading: viewModel.isQuoting,
                    enabled: canContinue,
                    action: onContinue
                )
                .lineLimit(1)
                .minimumScaleFactor(0.85)
            }
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface)
    }
}

#if DEBUG
    struct BookingSheetView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSheetView(
                vm: BookingViewModel(),
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider(),
                paymentSheet: StripePaymentController(),
                orderClient: LiveOrderClient(),
                onDismiss: {}
            )
        }
    }
#endif
