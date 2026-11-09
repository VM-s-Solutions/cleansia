import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import WebKit
import XCTest
@testable import CleansiaPartner

/// Native captures of an open legal document, the page `HtmlContentView` draws in full, from a fixed synthetic
/// text. Named `legal-<state>-ios-<w>x<h>-<lang>-<theme>`; they run with the Orders captures
/// (`TEST_RUNNER_PARTNER_ORDERS_CAPTURE=1`).
@MainActor
final class PartnerLegalCaptureTests: XCTestCase {
    private let phone = CGSize(width: 393, height: 852)
    private let small = CGSize(width: 375, height: 667)
    private var restoreBundle: Bundle!
    private var restoreTag: String!

    override func setUpWithError() throws {
        try super.setUpWithError()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
        try XCTSkipUnless(
            ProcessInfo.processInfo.environment["PARTNER_ORDERS_CAPTURE"] == "1",
            "captures run on request"
        )
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    func testLegalDocument() async throws {
        for (state, language, theme, size) in [
            ("open", "en", "light", phone), ("open", "en", "dark", phone), ("open", "uk", "light", phone),
            ("open", "en", "light", small), ("accepted", "en", "light", phone)
        ] {
            try use(language)
            let document = Self.document(language: language, accepted: state == "accepted")
            let name = "legal-\(state)-ios-\(Int(size.width))x\(Int(size.height))-\(language)-\(theme)"
            await capture(name: name, size: size) {
                ZStack(alignment: .bottom) {
                    Color.black.opacity(0.4)
                    NavigationStack {
                        LegalDocumentSheetContent(document: document)
                            .navigationTitle(document.title)
                            .navigationBarTitleDisplayMode(.inline)
                            .toolbar {
                                ToolbarItem(placement: .cancellationAction) {
                                    Button(L10n.close) {}
                                }
                            }
                            .background(CleansiaColors.surface.ignoresSafeArea())
                    }
                    .frame(width: size.width, height: size.height - 62)
                    .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
                .frame(width: size.width, height: size.height)
                .background(CleansiaColors.background)
                .tint(CleansiaColors.primaryText)
                .environment(\.locale, Locale(identifier: language))
                .preferredColorScheme(theme == "dark" ? .dark : .light)
                .dynamicTypeSize(.large)
            }
        }
    }

    private func use(_ language: String) throws {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
        CoreL10n.apply(languageTag: language)
    }

    private func capture(name: String, size: CGSize, @ViewBuilder content: () -> some View) async {
        let previous = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        let host = UIHostingController(rootView: content())
        if #available(iOS 16.4, *) { host.safeAreaRegions = [] }
        let window = previous?.windowScene.map(UIWindow.init(windowScene:)) ?? UIWindow(frame: CGRect(
            origin: .zero,
            size: size
        ))
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
        CaptureProbe.deviceInsets(host, in: window, homeIndicator: size.height == small.height ? 0 : 34)
        defer {
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        for _ in 0 ..< 16 {
            try? await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
        let loaded = Date().addingTimeInterval(6)
        while await !Self.documentLoaded(in: host.view), Date() < loaded {
            try? await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
        for _ in 0 ..< 8 {
            try? await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
        CaptureProbe.attach(window, name: name, to: self)
    }

    /// The document has laid its text out and handed the scroll view that height: a blank page has a height too.
    private static func documentLoaded(in view: UIView) async -> Bool {
        guard let web = webView(in: view) else { return false }
        let laidOut = "document.fonts.status == 'loaded' && document.body.firstChild ? document.body.scrollHeight : 0"
        guard let height = try? await web.evaluateJavaScript(laidOut) as? Double, height > 1 else { return false }
        return web.scrollView.contentSize.height >= height - 1
    }

    private static func webView(in view: UIView) -> WKWebView? {
        if let web = view as? WKWebView { return web }
        return view.subviews.lazy.compactMap(webView(in:)).first
    }

    static func document(language: String, accepted: Bool) -> CleanerLegalDocument {
        CleanerLegalDocument(
            type: ._3,
            legalDocumentTextId: "text-1",
            version: "2026-10-27",
            title: language == "uk" ? "Рамковий договір" : "Framework contract",
            contentHtml: language == "uk" ? ukrainian : english,
            isAccepted: accepted,
            acceptedVersion: accepted ? "2026-10-27" : nil,
            acceptedAt: accepted ? OrdersDesignFixture.at(28, 18, 4, month: 10) : nil
        )
    }

    private static let english = "<h2>1. Parties</h2><p>This framework contract is concluded between the operating"
        + " company that sells cleaning to its customers (the <strong>client</strong>) and you, the cleaner who"
        + " accepts it in the Cleansia partner app (the <strong>contractor</strong>).</p><h3>1.1 The client</h3>"
        + "<p>[company legal name], company ID [ID number], registered office [registered office].</p>"
        + "<h2>2. What you agree to</h2><ol><li>You take only the jobs you choose, and you may decline any job"
        + " before you accept its contract for work.</li><li>You carry out each job yourself, with care, at the"
        + " agreed time and place.</li><li>You keep the customer's address, access details and personal data"
        + " confidential.</li></ol><h2>3. Pay</h2><p>Your reward for each job is stated in its contract for work"
        + " before you accept it. Payouts are made monthly to the bank account in your profile.</p><ul><li>Cash a"
        + " customer pays you is counted against your next payout.</li><li>A job cancelled less than 24 hours"
        + " before its start is paid as set out in the cancellation rules.</li></ul><h2>4. Ending this"
        + " contract</h2><p>Either party may end this contract at any time with written notice. Jobs already"
        + " accepted are completed under their own contracts for work.</p>"

    private static let ukrainian = "<h2>1. Сторони</h2><p>Цей рамковий договір укладено між операційною"
        + " компанією, яка продає прибирання своїм клієнтам (<strong>замовник</strong>), і вами, прибиральницею,"
        + " яка приймає його в партнерському застосунку Cleansia (<strong>виконавець</strong>).</p>"
        + "<h3>1.1 Замовник</h3><p>[повна назва компанії], ідентифікаційний номер [номер], зареєстрований офіс"
        + " [адреса].</p><h2>2. Ваші зобовʼязання</h2><ol><li>Ви берете лише ті замовлення, які обираєте, і можете"
        + " відмовитися від будь-якого до прийняття договору підряду.</li><li>Ви виконуєте кожне замовлення"
        + " особисто, сумлінно, у погоджений час і в погодженому місці.</li><li>Ви зберігаєте в таємниці адресу"
        + " клієнта, дані для доступу та персональні дані.</li></ol><h2>3. Оплата</h2><p>Винагороду за кожне"
        + " замовлення зазначено в його договорі підряду ще до прийняття. Виплати здійснюються щомісяця на"
        + " банківський рахунок із вашого профілю.</p><ul><li>Готівку, яку вам платить клієнт, буде зараховано в"
        + " наступну виплату.</li><li>Замовлення, скасоване менш ніж за 24 години до початку, оплачується за"
        + " правилами скасування.</li></ul>"
}
